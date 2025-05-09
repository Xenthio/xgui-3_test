using System;
using System.Collections.Generic;
using System.Text;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Win32;

public class PELoader
{
	public bool Load( byte[] fileBytes, X86Core core, out uint entryPoint,
					 out Dictionary<string, uint> imports, out Dictionary<string, string> importSourceDlls )
	{
		entryPoint = 0;
		imports = new();
		importSourceDlls = new();

		// 1. Check MZ header
		if ( fileBytes.Length < 0x40 || fileBytes[0] != 'M' || fileBytes[1] != 'Z' )
			return false;

		// 2. Find PE header offset
		uint peOffset = BitConverter.ToUInt32( fileBytes, 0x3C );
		if ( peOffset + 0x40 > fileBytes.Length )
			return false;

		// 3. Check PE signature
		if ( fileBytes[peOffset] != 'P' || fileBytes[peOffset + 1] != 'E' )
			return false;

		// 4. Get image base and entry point RVA
		uint imageBase = BitConverter.ToUInt32( fileBytes, (int)peOffset + 0x34 );
		uint entryRVA = BitConverter.ToUInt32( fileBytes, (int)peOffset + 0x28 );
		entryPoint = imageBase + entryRVA;

		// 5. Parse section headers
		ushort numSections = BitConverter.ToUInt16( fileBytes, (int)peOffset + 6 );
		uint sectionHeadersOffset = peOffset + 24 + BitConverter.ToUInt16( fileBytes, (int)peOffset + 20 );

		for ( int i = 0; i < numSections; i++ )
		{
			uint s = sectionHeadersOffset + (uint)(i * 40);
			uint virtualAddress = BitConverter.ToUInt32( fileBytes, (int)s + 12 );
			uint virtualSize = BitConverter.ToUInt32( fileBytes, (int)s + 8 );
			uint rawDataPtr = BitConverter.ToUInt32( fileBytes, (int)s + 20 );
			uint rawDataSize = BitConverter.ToUInt32( fileBytes, (int)s + 16 );
			string sectionName = Encoding.ASCII.GetString( fileBytes, (int)s, 8 ).TrimEnd( '\0' );
			uint sectionFlags = BitConverter.ToUInt32( fileBytes, (int)s + 36 );

			// Copy raw data from file
			for ( uint j = 0; j < rawDataSize; j++ )
			{
				if ( rawDataPtr + j < fileBytes.Length )
					core.WriteByte( imageBase + virtualAddress + j, fileBytes[rawDataPtr + j], protect: false );
			}
			// Zero-fill the rest of the virtual size
			for ( uint j = rawDataSize; j < virtualSize; j++ )
			{
				core.WriteByte( imageBase + virtualAddress + j, 0, protect: false );
			}

			// Mark memory with appropriate protection
			if ( (sectionFlags & 0x20) != 0 ) // Contains code (IMAGE_SCN_CNT_CODE)
			{
				bool isWriteable = (sectionFlags & 0x80000000) != 0; // IMAGE_SCN_MEM_WRITE

				if ( isWriteable )
				{
					// Section contains both code AND is writeable
					Log.Info( $"Section {sectionName} at 0x{imageBase + virtualAddress:X8} is both code and writeable" );
					// Don't mark as code-only if it's meant to be written to
				}
				else
				{
					// Code-only section, mark as read-execute only
					core.MarkMemoryAsCode( imageBase + virtualAddress, virtualSize );
				}
			}
		}

		// 6. Parse imports
		ParseImports( fileBytes, peOffset, imageBase, core, imports, importSourceDlls );

		return true;
	}
	public class PEResourceEntry
	{
		public uint Type;
		public uint Name;
		public uint Language;
		public byte[] Data;
	}

	public bool ParseAllResources( byte[] fileBytes, out List<PEResourceEntry> resources )
	{
		var resourceList = new List<PEResourceEntry>();

		// 1. Find PE header
		if ( fileBytes.Length < 0x40 || fileBytes[0] != 'M' || fileBytes[1] != 'Z' )
		{
			resources = resourceList;
			Log.Info( "[Resource] Invalid PE file" );
			return false;
		}

		uint peOffset = BitConverter.ToUInt32( fileBytes, 0x3C );
		if ( peOffset + 0x40 > fileBytes.Length )
		{
			resources = resourceList;
			Log.Info( "[Resource] PE header not found" );
			return false;
		}

		// 2. Find resource directory RVA
		uint optHeaderOffset = peOffset + 24;
		ushort magic = BitConverter.ToUInt16( fileBytes, (int)optHeaderOffset );
		if ( magic != 0x10B )
		{
			resources = resourceList;
			Log.Info( "[Resource] Only PE32 supported" );
			return false; // Only PE32 supported
		}

		uint resourceDirOffset = optHeaderOffset + 96 + 2 * 8; // DataDirectory[2] is .rsrc
		uint resourceRVA = BitConverter.ToUInt32( fileBytes, (int)resourceDirOffset );
		uint resourceSize = BitConverter.ToUInt32( fileBytes, (int)resourceDirOffset + 4 );
		if ( resourceRVA == 0 || resourceSize == 0 )
		{
			resources = resourceList;
			Log.Info( "[Resource] No resources found" );
			return false;
		}

		// 3. Map RVA to file offset
		ushort numSections = BitConverter.ToUInt16( fileBytes, (int)peOffset + 6 );
		uint sectionHeadersOffset = peOffset + 24 + BitConverter.ToUInt16( fileBytes, (int)peOffset + 20 );
		uint rsrcRaw = 0, rsrcVA = 0, rsrcSize = 0;
		for ( int i = 0; i < numSections; i++ )
		{
			uint s = sectionHeadersOffset + (uint)(i * 40);
			string name = Encoding.ASCII.GetString( fileBytes, (int)s, 8 ).TrimEnd( '\0' );
			if ( name == ".rsrc" )
			{
				rsrcVA = BitConverter.ToUInt32( fileBytes, (int)s + 12 );
				rsrcSize = BitConverter.ToUInt32( fileBytes, (int)s + 8 );
				rsrcRaw = BitConverter.ToUInt32( fileBytes, (int)s + 20 );
				break;
			}
		}
		if ( rsrcRaw == 0 )
		{
			resources = resourceList;
			return false;
		}

		uint RvaToFile( uint rva )
		{
			if ( rva >= rsrcVA && rva < rsrcVA + rsrcSize )
				return rsrcRaw + (rva - rsrcVA);
			return rva;
		}

		// 4. Parse resource directory tree (recursive)
		void ParseDir( uint dirOffset, uint type, uint name, uint lang )
		{
			if ( dirOffset + 16 > fileBytes.Length ) return;
			int entryCount = BitConverter.ToUInt16( fileBytes, (int)dirOffset + 12 ) + BitConverter.ToUInt16( fileBytes, (int)dirOffset + 14 );
			for ( int i = 0; i < entryCount; i++ )
			{
				uint entryOffset = dirOffset + 16 + (uint)(i * 8);
				if ( entryOffset + 8 > fileBytes.Length ) continue;
				uint idOrName = BitConverter.ToUInt32( fileBytes, (int)entryOffset );
				uint dataOrSubdir = BitConverter.ToUInt32( fileBytes, (int)entryOffset + 4 );

				bool isNamed = (idOrName & 0x80000000) != 0;
				uint id = idOrName & 0x7FFFFFFF;

				bool isSubdir = (dataOrSubdir & 0x80000000) != 0;
				uint offset = dataOrSubdir & 0x7FFFFFFF;

				if ( isSubdir )
				{
					uint subdirOffset = rsrcRaw + offset;
					if ( type == 0 ) // First level: type
						ParseDir( subdirOffset, id, name, lang );
					else if ( name == 0 ) // Second level: name/id
						ParseDir( subdirOffset, type, id, lang );
					else // Third level: language
						ParseDir( subdirOffset, type, name, id );
				}
				else
				{
					uint dataEntryOffset = rsrcRaw + offset;
					if ( dataEntryOffset + 16 > fileBytes.Length ) continue;
					uint dataRVA = BitConverter.ToUInt32( fileBytes, (int)dataEntryOffset );
					uint dataSize = BitConverter.ToUInt32( fileBytes, (int)dataEntryOffset + 4 );
					uint dataFileOffset = RvaToFile( dataRVA );
					if ( dataFileOffset + dataSize > fileBytes.Length ) continue;
					var data = new byte[dataSize];
					Array.Copy( fileBytes, dataFileOffset, data, 0, dataSize );
					resourceList.Add( new PEResourceEntry { Type = type, Name = name, Language = lang, Data = data } );
				}
			}
		}

		ParseDir( rsrcRaw, 0, 0, 0 );
		resources = resourceList;
		return resources.Count > 0;
	}

	private void ParseImports( byte[] fileBytes, uint peOffset, uint imageBase, X86Core core,
							  Dictionary<string, uint> imports, Dictionary<string, string> importSourceDlls )
	{
		uint optHeaderOffset = peOffset + 24;
		ushort magic = BitConverter.ToUInt16( fileBytes, (int)optHeaderOffset );
		if ( magic != 0x10B ) return;

		uint importDirOffset = optHeaderOffset + 104;
		uint importDirRVA = BitConverter.ToUInt32( fileBytes, (int)importDirOffset );
		if ( importDirRVA == 0 ) return;

		// Section headers for RVA-to-file-offset mapping
		ushort numSections = BitConverter.ToUInt16( fileBytes, (int)peOffset + 6 );
		uint sectionHeadersOffset = peOffset + 24 + BitConverter.ToUInt16( fileBytes, (int)peOffset + 20 );
		var sectionHeaders = new List<(uint RVA, uint Size, uint Raw, uint RawSize)>();
		for ( int i = 0; i < numSections; i++ )
		{
			uint s = sectionHeadersOffset + (uint)(i * 40);
			uint va = BitConverter.ToUInt32( fileBytes, (int)s + 12 );
			uint vs = BitConverter.ToUInt32( fileBytes, (int)s + 8 );
			uint raw = BitConverter.ToUInt32( fileBytes, (int)s + 20 );
			uint rawSize = BitConverter.ToUInt32( fileBytes, (int)s + 16 );
			sectionHeaders.Add( (va, vs, raw, rawSize) );
		}
		uint RvaToFile( uint rva )
		{
			foreach ( var s in sectionHeaders )
				if ( rva >= s.RVA && rva < s.RVA + s.Size )
					return s.Raw + (rva - s.RVA);
			return rva;
		}

		uint importDescOffset = RvaToFile( importDirRVA );
		uint nextApiId = 0xFFFF0001;
		int descSize = 20;
		for ( int desc = 0; ; desc++ )
		{
			int baseOff = (int)importDescOffset + desc * descSize;
			if ( baseOff + descSize > fileBytes.Length ) break;
			uint nameRVA = BitConverter.ToUInt32( fileBytes, baseOff + 12 );
			if ( nameRVA == 0 ) break; // End of descriptors

			string dllName = ReadStringFromFile( fileBytes, (int)RvaToFile( nameRVA ) );
			uint thunkRVA = BitConverter.ToUInt32( fileBytes, baseOff ); // OriginalFirstThunk
			uint iatRVA = BitConverter.ToUInt32( fileBytes, baseOff + 16 ); // FirstThunk
			if ( thunkRVA == 0 ) thunkRVA = iatRVA;
			if ( thunkRVA == 0 ) continue;

			uint thunkOff = RvaToFile( thunkRVA );
			uint iatOff = RvaToFile( iatRVA );

			for ( int t = 0; ; t++ )
			{
				int thunkEntry = (int)thunkOff + t * 4;
				int iatEntry = (int)iatOff + t * 4;
				if ( thunkEntry + 4 > fileBytes.Length ) break;
				uint thunkData = BitConverter.ToUInt32( fileBytes, thunkEntry );
				if ( thunkData == 0 ) break;

				if ( (thunkData & 0x80000000) == 0 )
				{
					uint hintNameRVA = thunkData & 0x7FFFFFFF;
					int nameOff = (int)RvaToFile( hintNameRVA ) + 2; // skip hint
					string funcName = ReadStringFromFile( fileBytes, nameOff );

					// Assign a unique address for this import
					imports[funcName] = nextApiId;
					importSourceDlls[funcName] = dllName.ToUpper(); // Store which DLL each function comes from

					core.LogVerbose( $"Importing {funcName} from {dllName} at {nextApiId:X8}" );

					// Patch IAT in emulated memory
					core.WriteDword( imageBase + iatRVA + (uint)(t * 4), nextApiId, false );

					nextApiId++;
				}
			}
		}
	}

	private string ReadStringFromFile( byte[] fileBytes, int offset )
	{
		var bytes = new List<byte>();
		while ( offset < fileBytes.Length && fileBytes[offset] != 0 )
			bytes.Add( fileBytes[offset++] );
		return Encoding.ASCII.GetString( bytes.ToArray() );
	}
}
