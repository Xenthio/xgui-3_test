using System;
using System.Collections.Generic;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public class X86Core
{
	public const int PageSize = 4096;
	private readonly Dictionary<uint, byte[]> _memoryPages = new();
	private readonly Dictionary<uint, MemoryProtection> _pageProtection = new();

	// Stack frame information tracking - still useful for debugging
	private readonly Dictionary<uint, uint> _stackFrameInfo = new();

	public readonly Dictionary<string, uint> Registers = new()
	{
		{ "eax", 0 }, { "ebx", 0 }, { "ecx", 0 }, { "edx", 0 },
		{ "esi", 0 }, { "edi", 0 }, { "ebp", 0 }, { "esp", 0x00080000 },
		{ "eip", 0 }
	};

	// Flags
	public bool ZeroFlag, CarryFlag, SignFlag, OverflowFlag, DirectionFlag, InterruptFlag, ParityFlag;

	// Memory protection flags
	public enum MemoryProtection
	{
		ReadWrite,  // Normal data pages
		ReadOnly,   // Code pages
		ReadExecute // Code that can be executed
	}

	// Stack layout tracking
	private uint _stackBase = 0x00080000; // Initial ESP

	// Check if address is on the stack - useful for debugging
	public bool IsStackAddress( uint address )
	{
		return address >= 0x00070000 && address < 0x00090000;
	}

	// Memory operations - no correction, just like a real CPU
	public byte ReadByte( uint address )
	{
		// Direct memory access with no translation
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

		// Check page protection
		if ( _pageProtection.TryGetValue( page, out var protection ) )
		{
			if ( protection == MemoryProtection.ReadOnly ||
				 protection == MemoryProtection.ReadExecute )
			{
				throw new AccessViolationException(
					$"Write access violation at 0x{address:X8}" );
			}
		}

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

	// Track function entry (CALL instruction)
	public void EnterFunction( uint returnAddress )
	{
		uint currentESP = Registers["esp"];
		_stackFrameInfo[currentESP] = returnAddress;
		Log.Info( $"Function entered: ESP=0x{currentESP:X8}, EBP=0x{Registers["ebp"]:X8}, EIP=0x{Registers["eip"]:X8}, Return=0x{returnAddress:X8}" );
	}

	// Track stack frame setup (PUSH EBP, MOV EBP,ESP)
	public void SetupStackFrame()
	{
		uint oldEBP = Registers["ebp"];
		Registers["ebp"] = Registers["esp"];
		Log.Info( $"Stack frame setup: New EBP=0x{Registers["ebp"]:X8}, Old EBP=0x{oldEBP:X8}" );
	}

	// Track function exit (RET instruction)
	public void ExitFunction()
	{
		Log.Info( $"Function exited: ESP=0x{Registers["esp"]:X8}, EBP=0x{Registers["ebp"]:X8}, EIP=0x{Registers["eip"]:X8}" );
	}

	public string ReadString( uint address )
	{
		// Direct memory access with no translation
		if ( address == 0 )
			return "";

		var result = new System.Text.StringBuilder();
		int maxLength = 1000; // Safety limit

		try
		{
			for ( int i = 0; i < maxLength; i++ )
			{
				byte b = ReadByte( address + (uint)i );
				if ( b == 0 )
					break;

				result.Append( (char)b );
			}

			return result.ToString();
		}
		catch ( Exception ex )
		{
			Log.Error( $"ReadString failed at 0x{address:X8}: {ex.Message}" );
			return "";
		}
	}

	// Mark pages as code during PE loading
	public void MarkMemoryAsCode( uint address, uint size )
	{
		for ( uint i = 0; i < size; i += PageSize )
		{
			uint page = (address + i) & ~(uint)(PageSize - 1);
			_pageProtection[page] = MemoryProtection.ReadExecute;
		}
	}
}
