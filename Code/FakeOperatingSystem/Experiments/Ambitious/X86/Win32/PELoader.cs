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

					Log.Info( $"Importing {funcName} from {dllName} at {nextApiId:X8}" );

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
