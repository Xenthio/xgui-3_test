using System;
using System.Collections.Generic;
using System.Text;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Win32;

public class PELoader
{
	public bool Load( byte[] fileBytes, X86Core core, out uint entryPoint, out Dictionary<string, uint> imports )
	{
		entryPoint = 0;
		imports = new();

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

			for ( uint j = 0; j < Math.Min( virtualSize, rawDataSize ); j++ )
			{
				if ( rawDataPtr + j < fileBytes.Length )
				{
					core.WriteByte( imageBase + virtualAddress + j, fileBytes[rawDataPtr + j] );
				}
			}
		}

		// 6. Parse imports
		ParseImports( fileBytes, peOffset, imageBase, core, imports );

		return true;
	}

	private void ParseImports( byte[] fileBytes, uint peOffset, uint imageBase, X86Core core, Dictionary<string, uint> imports )
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

					Log.Info( $"Importing {funcName} from {dllName} at {nextApiId:X8}" );

					// Patch IAT in emulated memory
					core.WriteDword( imageBase + iatRVA + (uint)(t * 4), nextApiId );

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
