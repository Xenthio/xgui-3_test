using System.Collections.Generic;
using System.Text;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public class X86Core
{
	public const int PageSize = 4096;
	private readonly Dictionary<uint, byte[]> _memoryPages = new();
	public readonly Dictionary<string, uint> Registers = new()
	{
		{ "eax", 0 }, { "ebx", 0 }, { "ecx", 0 }, { "edx", 0 },
		{ "esi", 0 }, { "edi", 0 }, { "ebp", 0 }, { "esp", 0x00080000 },
		{ "eip", 0 }
	};

	// Flags
	public bool ZeroFlag, CarryFlag, SignFlag, OverflowFlag;

	// Memory operations
	public byte ReadByte( uint address )
	{
		uint page = address & ~(uint)(PageSize - 1);
		int offset = (int)(address & (PageSize - 1));
		if ( !_memoryPages.TryGetValue( page, out var data ) )
		{
			data = new byte[PageSize];
			_memoryPages[page] = data;
		}
		return data[offset];
	}

	public void WriteByte( uint address, byte value )
	{
		uint page = address & ~(uint)(PageSize - 1);
		int offset = (int)(address & (PageSize - 1));
		if ( !_memoryPages.TryGetValue( page, out var data ) )
		{
			data = new byte[PageSize];
			_memoryPages[page] = data;
		}
		data[offset] = value;
	}

	public uint ReadDword( uint address )
	{
		return (uint)(
			ReadByte( address ) |
			(ReadByte( address + 1 ) << 8) |
			(ReadByte( address + 2 ) << 16) |
			(ReadByte( address + 3 ) << 24)
		);
	}

	public void WriteDword( uint address, uint value )
	{
		WriteByte( address, (byte)(value & 0xFF) );
		WriteByte( address + 1, (byte)((value >> 8) & 0xFF) );
		WriteByte( address + 2, (byte)((value >> 16) & 0xFF) );
		WriteByte( address + 3, (byte)((value >> 24) & 0xFF) );
	}

	public void Push( uint value )
	{
		Registers["esp"] -= 4;
		WriteDword( Registers["esp"], value );
	}

	public uint Pop()
	{
		uint value = ReadDword( Registers["esp"] );
		Registers["esp"] += 4;
		return value;
	}

	public string ReadString( uint address )
	{
		var bytes = new List<byte>();
		byte b;
		while ( (b = ReadByte( address++ )) != 0 )
			bytes.Add( b );
		return Encoding.ASCII.GetString( bytes.ToArray() );
	}
}
