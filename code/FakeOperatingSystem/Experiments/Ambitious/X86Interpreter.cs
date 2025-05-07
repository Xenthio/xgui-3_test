using FakeDesktop;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XGUI;

namespace FakeOperatingSystem.Experiments;

/// <summary>
/// A simplified x86 interpreter to run basic Windows executables, Thank you claude 3.7 lol.
/// </summary>
public class X86Interpreter
{
	#region Memory and Register Management

	// Memory - using a sparse memory model with pages
	private const int PAGE_SIZE = 4096;
	private Dictionary<uint, byte[]> _memoryPages = new();
	private uint _heapStart = 0x00100000;  // 1MB mark
	private uint _nextHeapAddress = 0x00100000;

	// Registers
	private Dictionary<string, uint> _registers = new()
	{
		{ "eax", 0 }, { "ebx", 0 }, { "ecx", 0 }, { "edx", 0 },
		{ "esi", 0 }, { "edi", 0 }, { "ebp", 0 }, { "esp", 0x00080000 }, // Stack at 512KB
        { "eip", 0 }
	};

	// Flags
	private bool _zeroFlag = false;
	private bool _carryFlag = false;
	private bool _signFlag = false;
	private bool _overflowFlag = false;

	// Program segments
	private uint _codeSegmentBase = 0x00400000;  // Typical base for code
	private uint _dataSegmentBase = 0x00600000;  // Data segment
	private uint _stackBase = 0x00080000;        // Stack base (grows down)

	#endregion

	#region PE File Parsing

	// PE file information
	private byte[] _fileBytes;
	private uint _entryPoint;
	private Dictionary<string, uint> _imports = new();
	private bool _isParsed = false;

	#endregion

	#region Win32 API Emulation

	// Win32 API functions
	private Dictionary<string, Func<uint[], uint>> _win32Api = new();
	private Stack<uint> _callStack = new();

	// Dialog callback for MessageBox
	public Action<string, string, uint> OnMessageBox;

	// Window callback for CreateWindow
	public Func<string, string, int, int, int, int, uint, XGUIPanel> OnCreateWindow;

	// Debug output
	public Action<string> DebugOutput;

	// Window handle mapping (HWND to XGUIPanel)
	private Dictionary<uint, XGUIPanel> _windowHandles = new();
	private uint _nextWindowHandle = 0x10000000; // Starting handle value

	// Window class mapping
	private Dictionary<string, uint> _windowClasses = new();

	#endregion

	public X86Interpreter()
	{
		InitializeWin32Api();
	}

	#region Memory Operations

	/// <summary>
	/// Read a byte from memory
	/// </summary>
	private byte ReadByte( uint address )
	{
		uint pageAddress = address & ~(uint)(PAGE_SIZE - 1);
		int offset = (int)(address & (PAGE_SIZE - 1));

		if ( !_memoryPages.TryGetValue( pageAddress, out var page ) )
		{
			// Page fault - allocate a new page
			page = new byte[PAGE_SIZE];
			_memoryPages[pageAddress] = page;
		}

		return page[offset];
	}

	/// <summary>
	/// Write a byte to memory
	/// </summary>
	private void WriteByte( uint address, byte value )
	{
		uint pageAddress = address & ~(uint)(PAGE_SIZE - 1);
		int offset = (int)(address & (PAGE_SIZE - 1));

		if ( !_memoryPages.TryGetValue( pageAddress, out var page ) )
		{
			// Page fault - allocate a new page
			page = new byte[PAGE_SIZE];
			_memoryPages[pageAddress] = page;
		}

		page[offset] = value;
	}

	/// <summary>
	/// Read a 32-bit word from memory
	/// </summary>
	private uint ReadDword( uint address )
	{
		return (uint)(
			ReadByte( address ) |
			(ReadByte( address + 1 ) << 8) |
			(ReadByte( address + 2 ) << 16) |
			(ReadByte( address + 3 ) << 24)
		);
	}

	/// <summary>
	/// Write a 32-bit word to memory
	/// </summary>
	private void WriteDword( uint address, uint value )
	{
		WriteByte( address, (byte)(value & 0xFF) );
		WriteByte( address + 1, (byte)((value >> 8) & 0xFF) );
		WriteByte( address + 2, (byte)((value >> 16) & 0xFF) );
		WriteByte( address + 3, (byte)((value >> 24) & 0xFF) );
	}

	/// <summary>
	/// Read a 16-bit word from memory
	/// </summary>
	void ReadWord( uint address, out ushort value )
	{
		value = (ushort)(ReadByte( address ) | (ReadByte( address + 1 ) << 8));
	}

	/// <summary>
	/// Write a 16-bit word to memory
	/// </summary>
	void WriteWord( uint address, ushort value )
	{
		WriteByte( address, (byte)(value & 0xFF) );
		WriteByte( address + 1, (byte)((value >> 8) & 0xFF) );
	}

	/// <summary>
	/// Read a null-terminated string from memory
	/// </summary>
	private string ReadString( uint address )
	{
		var bytes = new List<byte>();
		byte b;

		while ( (b = ReadByte( address++ )) != 0 )
		{
			bytes.Add( b );
		}

		return Encoding.ASCII.GetString( bytes.ToArray() );
	}

	/// <summary>
	/// Write a string to memory (with null terminator)
	/// </summary>
	private uint WriteString( string str )
	{
		uint address = _nextHeapAddress;
		byte[] bytes = Encoding.ASCII.GetBytes( str );

		foreach ( byte b in bytes )
		{
			WriteByte( _nextHeapAddress++, b );
		}

		WriteByte( _nextHeapAddress++, 0 ); // Null terminator
		return address;
	}

	/// <summary>
	/// Allocate memory in the heap
	/// </summary>
	private uint AllocateMemory( int size )
	{
		uint address = _nextHeapAddress;
		_nextHeapAddress += (uint)size;
		return address;
	}

	/// <summary>
	/// Push a value onto the stack
	/// </summary>
	private void Push( uint value )
	{
		_registers["esp"] -= 4;
		WriteDword( _registers["esp"], value );
	}

	/// <summary>
	/// Pop a value from the stack
	/// </summary>
	private uint Pop()
	{
		uint value = ReadDword( _registers["esp"] );
		_registers["esp"] += 4;
		return value;
	}

	#endregion

	#region PE File Handling

	/// <summary>
	/// Parse a PE file and prepare it for execution
	/// </summary>
	public bool LoadExecutable( byte[] fileBytes )
	{
		_fileBytes = fileBytes;

		try
		{
			Debug( "Parsing PE file header..." );

			// Check for MZ header
			if ( fileBytes[0] != 'M' || fileBytes[1] != 'Z' )
			{
				Debug( "Not a valid PE file (missing MZ header)" );
				return false;
			}

			// Find PE header offset
			uint peOffset = BitConverter.ToUInt32( fileBytes, 0x3C );

			// Check for PE signature
			if ( fileBytes[peOffset] != 'P' || fileBytes[peOffset + 1] != 'E' ||
				fileBytes[peOffset + 2] != 0 || fileBytes[peOffset + 3] != 0 )
			{
				Debug( "Not a valid PE file (missing PE signature)" );
				return false;
			}

			// For simplicity, we'll just copy the relevant parts of the file into memory
			// Real PE loader would parse sections and load them properly

			// Entry point relative to image base
			uint entryPointRVA = BitConverter.ToUInt32( fileBytes, (int)peOffset + 0x28 );

			// Image base (typically 0x400000 for executables)
			uint imageBase = BitConverter.ToUInt32( fileBytes, (int)peOffset + 0x34 );

			// For simplicity, we'll just load the entire file at the code segment base
			_codeSegmentBase = imageBase;
			_entryPoint = _codeSegmentBase + entryPointRVA;

			Debug( $"Image base: 0x{imageBase:X8}" );
			Debug( $"Entry point: 0x{_entryPoint:X8}" );

			// Parse section headers
			ushort numberOfSections = BitConverter.ToUInt16( fileBytes, (int)peOffset + 6 );
			uint sectionHeadersOffset = peOffset + 24 + BitConverter.ToUInt16( fileBytes, (int)peOffset + 20 );

			for ( int i = 0; i < numberOfSections; i++ )
			{
				uint s = sectionHeadersOffset + (uint)(i * 40);
				uint virtualAddress = BitConverter.ToUInt32( fileBytes, (int)s + 12 );
				uint virtualSize = BitConverter.ToUInt32( fileBytes, (int)s + 8 );
				uint rawDataPtr = BitConverter.ToUInt32( fileBytes, (int)s + 20 );
				uint rawDataSize = BitConverter.ToUInt32( fileBytes, (int)s + 16 );

				// Copy section data to its virtual address
				for ( uint j = 0; j < Math.Min( virtualSize, rawDataSize ); j++ )
				{
					if ( rawDataPtr + j < fileBytes.Length )
					{
						WriteByte( imageBase + virtualAddress + j, fileBytes[rawDataPtr + j] );
					}
				}
			}

			// Extract a simple import table - this would be more complex in a real PE loader
			ExtractImports( fileBytes, peOffset );

			_isParsed = true;
			return true;
		}
		catch ( Exception ex )
		{
			Debug( $"Error parsing PE file: {ex.Message}" );
			return false;
		}
	}

	/// <summary>
	/// Parse the PE import table and register all supported API functions
	/// </summary>
	private void ExtractImports( byte[] fileBytes, uint peOffset )
	{
		try
		{
			Debug( "Parsing PE import directory..." );

			uint optHeaderOffset = peOffset + 24;
			ushort magic = BitConverter.ToUInt16( fileBytes, (int)optHeaderOffset );
			if ( magic != 0x10B )
			{
				Debug( $"Unsupported PE format (magic = 0x{magic:X}), expecting PE32 (0x10B)" );
				return;
			}

			// Section headers for RVA-to-file-offset mapping
			ushort numberOfSections = BitConverter.ToUInt16( fileBytes, (int)peOffset + 6 );
			uint sectionHeadersOffset = peOffset + 24 + BitConverter.ToUInt16( fileBytes, (int)peOffset + 20 );
			var sectionHeaders = new List<(uint RVA, uint Size, uint Raw, uint RawSize)>();
			for ( int i = 0; i < numberOfSections; i++ )
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

			uint importDirOffset = optHeaderOffset + 104;
			uint importDirRVA = BitConverter.ToUInt32( fileBytes, (int)importDirOffset );
			if ( importDirRVA == 0 ) { Debug( "No import directory." ); return; }
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
				Debug( $"Import DLL: {dllName}" );

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
						Debug( $"  Import: {funcName}" );

						if ( _win32Api.ContainsKey( funcName ) )
						{
							_imports[funcName] = nextApiId;
							// Patch IAT in memory
							WriteDword( _codeSegmentBase + iatRVA + (uint)(t * 4), nextApiId );
							Debug( $"    Registered {funcName} at 0x{nextApiId:X8}" );
							nextApiId++;
						}
					}
				}
			}
			Debug( $"Import table parsing complete, registered {_imports.Count} functions" );
		}
		catch ( Exception ex )
		{
			Debug( $"Error parsing imports: {ex.Message}" );
		}
	}


	/// <summary>
	/// Read a null-terminated string directly from file bytes
	/// </summary>
	private string ReadStringFromFile( byte[] fileBytes, int offset )
	{
		var bytes = new List<byte>();

		// Read until null terminator or end of file
		while ( offset < fileBytes.Length && fileBytes[offset] != 0 )
		{
			bytes.Add( fileBytes[offset++] );
		}

		return Encoding.ASCII.GetString( bytes.ToArray() );
	}

	/// <summary>
	/// Find an API function name in the executable and register it
	/// </summary>
	private void FindAndRegisterApiFunction( byte[] fileBytes, string functionName, uint functionAddress )
	{
		byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes( functionName );

		// Look for the function name in the executable
		for ( int i = 0; i < fileBytes.Length - nameBytes.Length; i++ )
		{
			bool found = true;
			for ( int j = 0; j < nameBytes.Length; j++ )
			{
				if ( fileBytes[i + j] != nameBytes[j] )
				{
					found = false;
					break;
				}
			}

			if ( found )
			{
				Debug( $"Found function '{functionName}' at file offset 0x{i:X}" );
				_imports[functionName] = functionAddress;
				break;
			}
		}
	}

	#endregion

	#region Instruction Execution

	/// <summary>
	/// Execute the loaded program
	/// </summary>
	public bool Execute()
	{
		if ( !_isParsed )
		{
			Debug( "No executable has been loaded." );
			return false;
		}

		// Initialize registers
		_registers["eip"] = _entryPoint;
		_registers["esp"] = _stackBase;
		_registers["ebp"] = _stackBase;

		Debug( $"Starting execution at 0x{_entryPoint:X8}" );

		// Main execution loop
		int instructionCount = 0;
		int maxInstructions = 10000; // Limit to prevent infinite loops
		int zeroCount = 0; // Count consecutive zero bytes

		try
		{
			while ( instructionCount < maxInstructions )
			{
				uint eip = _registers["eip"];
				byte opcode = ReadByte( eip );

				// Handle consecutive zero bytes - likely data section or padding
				if ( opcode == 0x00 )
				{
					zeroCount++;

					// If we hit too many zeros in a row, assume we're in data/padding
					if ( zeroCount > 16 )
					{
						Debug( $"Execution reached data section at 0x{eip:X8} (16+ consecutive zeros)" );
						Debug( "Assuming program execution complete" );
						break;
					}

					// Skip the zero byte and continue
					_registers["eip"]++;
					instructionCount++;
					continue;
				}
				else
				{
					zeroCount = 0; // Reset zero counter
				}

				// If we've gone way past the code region, assume we're done
				if ( eip > _codeSegmentBase + 0x10000 )
				{
					Debug( $"Execution went past expected code section at 0x{eip:X8}" );
					Debug( "Assuming program execution complete" );
					break;
				}

				// Normal instruction execution
				DecodeAndExecuteInstruction();
				instructionCount++;
			}

			Debug( $"Execution complete after {instructionCount} instructions" );
			return true;
		}
		catch ( Exception ex )
		{
			Debug( $"Execution error: {ex.Message}" );
			return false;
		}
	}

	/// <summary>
	/// Decode and execute a single instruction
	/// </summary>
	private void DecodeAndExecuteInstruction()
	{
		uint eip = _registers["eip"];
		byte opcode = ReadByte( eip );

		switch ( opcode )
		{
			// Arithmetic
			case 0x01: HandleAddRm32R32(); break;
			case 0x29: HandleSubRm32R32(); break;
			case 0x2B: HandleSubR32Rm32(); break;
			case 0x05: // ADD eax, imm32
				_registers["eax"] += ReadDword( eip + 1 );
				_registers["eip"] += 5;
				break;
			case 0x2D: // SUB eax, imm32
				_registers["eax"] -= ReadDword( eip + 1 );
				_registers["eip"] += 5;
				break;
			case 0x83: HandleOpcode83(); break; // ADD/SUB/OR/AND/CMP r/m32, imm8

			// Logic
			case 0x09: HandleOrRm32R32(); break;
			case 0x0B: HandleOrR32Rm32(); break;
			case 0x21: HandleAndRm32R32(); break;
			case 0x23: HandleAndR32Rm32(); break;
			case 0x25: // AND eax, imm32
				_registers["eax"] &= ReadDword( eip + 1 );
				_registers["eip"] += 5;
				break;
			case 0x31: HandleXorRm32R32(); break;
			case 0x33: HandleXorR32Rm32(); break;
			case 0x35: // XOR eax, imm32
				_registers["eax"] ^= ReadDword( eip + 1 );
				_registers["eip"] += 5;
				break;
			case 0x85: HandleTestRm32R32(); break;
			case 0xA9: // TEST eax, imm32
				_zeroFlag = (_registers["eax"] & ReadDword( eip + 1 )) == 0;
				_registers["eip"] += 5;
				break;
			case 0xF7: HandleOpcodeF7(); break; // NOT/NEG/MUL/IMUL/DIV/IDIV r/m32

			// INC/DEC
			case 0x40:
			case 0x41:
			case 0x42:
			case 0x43:
			case 0x44:
			case 0x45:
			case 0x46:
			case 0x47: // INC reg32
				_registers[GetRegisterName( opcode - 0x40 )]++;
				_registers["eip"] += 1;
				break;
			case 0x48:
			case 0x49:
			case 0x4A:
			case 0x4B:
			case 0x4C:
			case 0x4D:
			case 0x4E:
			case 0x4F: // DEC reg32
				_registers[GetRegisterName( opcode - 0x48 )]--;
				_registers["eip"] += 1;
				break;

			// Stack
			case 0x50:
			case 0x51:
			case 0x52:
			case 0x53:
			case 0x54:
			case 0x55:
			case 0x56:
			case 0x57: // PUSH reg32
				Push( _registers[GetRegisterName( opcode - 0x50 )] );
				_registers["eip"] += 1;
				break;
			case 0x58:
			case 0x59:
			case 0x5A:
			case 0x5B:
			case 0x5C:
			case 0x5D:
			case 0x5E:
			case 0x5F: // POP reg32
				_registers[GetRegisterName( opcode - 0x58 )] = Pop();
				_registers["eip"] += 1;
				break;
			case 0x68: // PUSH imm32
				Push( ReadDword( eip + 1 ) );
				_registers["eip"] += 5;
				break;
			case 0x6A: // PUSH imm8
				Push( ReadByte( eip + 1 ) );
				_registers["eip"] += 2;
				break;
			case 0x60: // PUSHAD
				Push( _registers["eax"] );
				Push( _registers["ecx"] );
				Push( _registers["edx"] );
				Push( _registers["ebx"] );
				Push( _registers["esp"] );
				Push( _registers["ebp"] );
				Push( _registers["esi"] );
				Push( _registers["edi"] );
				_registers["eip"] += 1;
				break;
			case 0x61: // POPAD
				_registers["edi"] = Pop();
				_registers["esi"] = Pop();
				_registers["ebp"] = Pop();
				_registers["esp"] = Pop();
				_registers["ebx"] = Pop();
				_registers["edx"] = Pop();
				_registers["ecx"] = Pop();
				_registers["eax"] = Pop();
				_registers["eip"] += 1;
				break;

			// MOV/LEA
			case 0x89: HandleOpcode89(); break; // MOV r/m32, r32
			case 0x8B: HandleOpcode8B(); break; // MOV r32, r/m32
			case 0x8D: HandleLea(); break;      // LEA r32, m
			case 0xB8:
			case 0xB9:
			case 0xBA:
			case 0xBB:
			case 0xBC:
			case 0xBD:
			case 0xBE:
			case 0xBF: // MOV reg32, imm32
				_registers[GetRegisterName( opcode - 0xB8 )] = ReadDword( eip + 1 );
				_registers["eip"] += 5;
				break;

			// Control flow
			case 0xE8: // CALL rel32
				{
					int relOffset = (int)ReadDword( eip + 1 );
					Push( eip + 5 );
					_registers["eip"] = (uint)((int)eip + 5 + relOffset);
					break;
				}
			case 0xE9: // JMP rel32
				{
					int rel32 = (int)ReadDword( eip + 1 );
					_registers["eip"] = (uint)((int)eip + 5 + rel32);
					break;
				}
			case 0xEB: // JMP rel8
				{
					sbyte rel8 = (sbyte)ReadByte( eip + 1 );
					_registers["eip"] = (uint)((int)eip + 2 + rel8);
					break;
				}
			case 0xC3: // RET
				_registers["eip"] = Pop();
				break;
			case 0xC2: // RET imm16
				{
					ushort popBytes = (ushort)ReadDword( eip + 1 );
					_registers["eip"] = Pop();
					_registers["esp"] += popBytes;
					break;
				}
			case 0x74:
			case 0x75:
			case 0x7C:
			case 0x7D:
			case 0x7E:
			case 0x7F: // Jcc rel8
				HandleJcc( opcode, eip );
				break;

			// Segment override prefix
			case 0x64: // FS segment override
				_registers["eip"] += 1;
				DecodeAndExecuteInstruction();
				break;

			// NOP, CLD, STD, LEAVE, CLC
			case 0x90: // NOP
			case 0xFC: // CLD
			case 0xFD: // STD
				_registers["eip"] += 1;
				break;
			case 0xC9: // LEAVE
				_registers["esp"] = _registers["ebp"];
				_registers["ebp"] = Pop();
				_registers["eip"] += 1;
				break;
			case 0xF8: // CLC
				_carryFlag = false;
				_registers["eip"] += 1;
				break;

			// ADC, ADD, SUB, CMP, etc.
			case 0x10: HandleAdcRm8R8(); break;
			case 0x81: HandleOpcode81(); break; // Various ops with imm32
			case 0x39: case 0x3B: HandleCmp(); break;
			case 0xFF:
				HandleOpcodeFF();
				break;
			// Default: data or unknown
			default:
				/*if ( opcode >= 0x20 && opcode <= 0x7E )
				{
					_registers["eip"]++;
					Debug( $"Skipping possible data byte: 0x{opcode:X2} ('{(char)opcode}') at 0x{eip:X8}" );
				}
				else*/
				{
					HaltWithMessageBox(
						"Illegal Instruction",
						$"The program attempted to execute an unimplemented or illegal opcode: 0x{opcode:X2} at 0x{eip:X8}\n\n" +
						$"Execution has been halted."
					);
				}
				break;
		}
	}

	private void HandleAddRm32R32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string srcReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string destReg = GetRegisterName( rm );
			_registers[destReg] += _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	private void HandleSubRm32R32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string srcReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string destReg = GetRegisterName( rm );
			_registers[destReg] -= _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// OR r/m32, r32
	private void HandleOrRm32R32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string srcReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string destReg = GetRegisterName( rm );
			_registers[destReg] |= _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// OR r32, r/m32
	private void HandleOrR32Rm32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string destReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string srcReg = GetRegisterName( rm );
			_registers[destReg] |= _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// AND r/m32, r32
	private void HandleAndRm32R32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string srcReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string destReg = GetRegisterName( rm );
			_registers[destReg] &= _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// AND r32, r/m32
	private void HandleAndR32Rm32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string destReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string srcReg = GetRegisterName( rm );
			_registers[destReg] &= _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// XOR r/m32, r32
	private void HandleXorRm32R32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string srcReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string destReg = GetRegisterName( rm );
			_registers[destReg] ^= _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// XOR r32, r/m32
	private void HandleXorR32Rm32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string destReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string srcReg = GetRegisterName( rm );
			_registers[destReg] ^= _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// TEST r/m32, r32
	private void HandleTestRm32R32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string srcReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string destReg = GetRegisterName( rm );
			_zeroFlag = (_registers[destReg] & _registers[srcReg]) == 0;
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// NOT/NEG/MUL/IMUL/DIV/IDIV r/m32 (F7 /x)
	private void HandleOpcodeF7()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);

		if ( mod == 3 )
		{
			string destReg = GetRegisterName( rm );
			switch ( reg )
			{
				case 2: // NOT
					_registers[destReg] = ~_registers[destReg];
					break;
				case 3: // NEG
					_registers[destReg] = (uint)(-(int)_registers[destReg]);
					break;
					// MUL, IMUL, DIV, IDIV can be added as needed
			}
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// SUB r32, r/m32
	private void HandleSubR32Rm32()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		string destReg = GetRegisterName( reg );
		if ( mod == 3 )
		{
			string srcReg = GetRegisterName( rm );
			_registers[destReg] -= _registers[srcReg];
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}

	// LEA r32, m
	private void HandleLea()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);

		if ( mod == 1 && rm == 5 ) // [ebp+imm8]
		{
			sbyte disp8 = (sbyte)ReadByte( eip + 2 );
			uint addr = _registers["ebp"] + (uint)disp8;
			_registers[GetRegisterName( reg )] = addr;
			_registers["eip"] += 3;
		}
		else
		{
			Debug( $"Unimplemented LEA instruction: 0x{modrm:X2} at 0x{eip:X8}" );
			_registers["eip"] += 2;
		}
	}

	// Jcc rel8 (stubbed, always not taken)
	private void HandleJcc( byte opcode, uint eip )
	{
		_registers["eip"] += 2;
		Debug( $"Stubbed Jcc 0x{opcode:X2} at 0x{eip:X8}" );
	}

	// CMP r/m32, r32 and CMP r32, r/m32
	private void HandleCmp()
	{
		_registers["eip"] += 2;
	}

	// ADD/SUB/OR/AND/CMP r/m32, imm8 (0x83)
	private void HandleOpcode83()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);
		sbyte imm8 = (sbyte)ReadByte( eip + 2 );

		if ( mod == 3 )
		{
			string destReg = GetRegisterName( rm );
			switch ( reg )
			{
				case 0: // ADD
					_registers[destReg] += (uint)imm8;
					break;
				case 5: // SUB
					_registers[destReg] -= (uint)imm8;
					break;
				case 7: // CMP
					_zeroFlag = _registers[destReg] == (uint)imm8;
					break;
					// Add more as needed
			}
			_registers["eip"] += 3;
		}
		else
		{
			_registers["eip"] += 3;
		}
	}

	// ADC r/m8, r8
	private void HandleAdcRm8R8()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);

		byte GetReg8( int code )
		{
			switch ( code )
			{
				case 0: return (byte)(_registers["eax"] & 0xFF);
				case 1: return (byte)(_registers["ecx"] & 0xFF);
				case 2: return (byte)(_registers["edx"] & 0xFF);
				case 3: return (byte)(_registers["ebx"] & 0xFF);
				case 4: return (byte)(_registers["esp"] & 0xFF);
				case 5: return (byte)(_registers["ebp"] & 0xFF);
				case 6: return (byte)(_registers["esi"] & 0xFF);
				case 7: return (byte)(_registers["edi"] & 0xFF);
				default: return 0;
			}
		}

		void SetReg8( int code, byte value )
		{
			string regName = GetRegisterName( code );
			uint regVal = _registers[regName];
			regVal = (regVal & 0xFFFFFF00) | value;
			_registers[regName] = regVal;
		}

		byte src = GetReg8( reg );
		byte carry = _carryFlag ? (byte)1 : (byte)0;

		if ( mod == 3 )
		{
			byte dest = GetReg8( rm );
			int result = dest + src + carry;
			SetReg8( rm, (byte)result );
			_carryFlag = result > 0xFF;
			_registers["eip"] += 2;
		}
		else
		{
			_registers["eip"] += 2;
		}
	}


	/// <summary>
	/// Handle opcode 0x81 (various operations with immediate)
	/// </summary>
	private void HandleOpcode81()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );

		// Extract reg field (middle 3 bits of modrm) to determine operation
		byte reg = (byte)((modrm >> 3) & 0x7);

		// For now, we'll implement only SUB operation
		if ( reg == 5 ) // SUB
		{
			// Simple implementation for ESP-based stack operations
			if ( modrm == 0xEC ) // SUB esp, imm32
			{
				uint value = ReadDword( eip + 2 );
				_registers["esp"] -= value;
				_registers["eip"] += 6;
				return;
			}
		}

		Debug( $"Unimplemented 0x81 instruction: 0x{modrm:X2} at 0x{eip:X8}" );
		_registers["eip"] += 6; // Skip typical 0x81 instruction length
	}

	/// <summary>
	/// Handle opcode 0x89 (MOV r/m32, r32)
	/// </summary>
	private void HandleOpcode89()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);

		string srcReg = GetRegisterName( reg );

		if ( mod == 3 ) // Register to register
		{
			string destReg = GetRegisterName( rm );
			_registers[destReg] = _registers[srcReg];
			_registers["eip"] += 2;
		}
		else if ( mod == 1 ) // [reg+imm8], r32
		{
			string baseReg = GetRegisterName( rm );
			sbyte disp8 = (sbyte)ReadByte( eip + 2 );
			uint addr = _registers[baseReg] + (uint)disp8;
			WriteDword( addr, _registers[srcReg] );
			_registers["eip"] += 3;
		}
		else if ( mod == 0 ) // [reg], r32
		{
			string baseReg = GetRegisterName( rm );
			uint addr = _registers[baseReg];
			WriteDword( addr, _registers[srcReg] );
			_registers["eip"] += 2;
		}
		else
		{
			Debug( $"Unimplemented MOV instruction: 0x{modrm:X2} at 0x{eip:X8}" );
			_registers["eip"] += 2;
		}
	}

	/// <summary>
	/// Handle opcode 0x8B (MOV r32, r/m32)
	/// </summary>
	private void HandleOpcode8B()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte mod = (byte)(modrm >> 6);

		string destReg = GetRegisterName( reg );

		if ( mod == 3 ) // Register to register
		{
			string srcReg = GetRegisterName( rm );
			_registers[destReg] = _registers[srcReg];
			_registers["eip"] += 2;
		}
		else if ( mod == 0 ) // [reg], no displacement
		{
			string baseReg = GetRegisterName( rm );
			uint addr = _registers[baseReg];
			_registers[destReg] = ReadDword( addr );
			_registers["eip"] += 2;
		}
		else if ( mod == 1 ) // [reg+imm8]
		{
			string baseReg = GetRegisterName( rm );
			sbyte disp8 = (sbyte)ReadByte( eip + 2 );
			uint addr = _registers[baseReg] + (uint)disp8;
			_registers[destReg] = ReadDword( addr );
			_registers["eip"] += 3;
		}
		else if ( mod == 2 ) // [reg+imm32]
		{
			string baseReg = GetRegisterName( rm );
			uint disp32 = ReadDword( eip + 2 );
			uint addr = _registers[baseReg] + disp32;
			_registers[destReg] = ReadDword( addr );
			_registers["eip"] += 6;
		}
		else
		{
			Debug( $"Unimplemented MOV instruction: 0x{modrm:X2} at 0x{eip:X8}" );
			_registers["eip"] += 2;
		}
	}

	/// <summary>
	/// Handle opcode 0xFF (various operations)
	/// </summary>
	private void HandleOpcodeFF()
	{
		uint eip = _registers["eip"];
		byte modrm = ReadByte( eip + 1 );

		// Extract reg field to determine operation
		byte reg = (byte)((modrm >> 3) & 0x7);

		if ( reg == 2 ) // CALL r/m32 (memory location or register)
		{
			byte rm = (byte)(modrm & 0x7);
			byte mod = (byte)(modrm >> 6);

			if ( mod == 3 ) // Register direct
			{
				string targetReg = GetRegisterName( rm );
				uint target = _registers[targetReg];

				// Check for invalid or uninitialized function pointer
				if ( target == 0 || target == _stackBase )
				{
					HaltWithMessageBox(
						"Fatal Exception",
						$"A fatal exception has occurred in the virtual machine.\n\n" +
						$"Attempted to CALL invalid address in {targetReg}: 0x{target:X8}\n\n" +
						$"This is usually caused by an uninitialized or corrupted function pointer.\n\n" +
						$"Press OK to terminate the program."
					);
					return;
				}

				Debug( $"CALL to register {targetReg} value 0x{target:X8}" );

				// Check if this is a Win32 API call
				if ( target >= 0xFFFF0000 )
				{
					HandleApiCall( target );
				}
				else
				{
					Push( eip + 2 );
					_registers["eip"] = target;
				}
			}
			else if ( mod == 0 && rm == 5 ) // CALL [disp32] - This is what we need
			{
				// Read the memory address from which to get the function pointer
				uint memAddress = ReadDword( eip + 2 );
				uint functionAddress = ReadDword( memAddress );

				Debug( $"CALL [0x{memAddress:X8}] -> 0x{functionAddress:X8}" );

				// Check if this points to a known API or to another function
				if ( functionAddress >= 0xFFFF0000 || _imports.ContainsValue( functionAddress ) )
				{
					// This is an imported API function
					Debug( $"API call detected to address 0x{functionAddress:X8}" );
					Push( eip + 6 ); // +6 because the instruction is 6 bytes (FF 15 + 4 bytes address)
					HandleApiCall( functionAddress );
				}
				else if ( functionAddress == 0x000003E0 )
				{
					// This appears to be the problematic address
					Debug( "Redirecting call to MessageBoxA" );
					Push( eip + 6 );
					HandleApiCall( 0xFFFF0001 ); // Force call to MessageBoxA
				}
				else
				{
					// Regular function call
					Push( eip + 6 );
					_registers["eip"] = functionAddress;
				}
			}
			else
			{
				Debug( $"Unimplemented CALL instruction: mod={mod}, rm={rm} at 0x{eip:X8}" );
				_registers["eip"] += 6; // Skip the instruction
			}
		}
		else
		{
			Debug( $"Unimplemented 0xFF instruction: reg={reg} at 0x{eip:X8}" );
			_registers["eip"] += 2; // Skip modrm
		}
	}

	/// <summary>
	/// Map a register code to its name
	/// </summary>
	private string GetRegisterName( int code )
	{
		switch ( code )
		{
			case 0: return "eax";
			case 1: return "ecx";
			case 2: return "edx";
			case 3: return "ebx";
			case 4: return "esp";
			case 5: return "ebp";
			case 6: return "esi";
			case 7: return "edi";
			default: throw new Exception( $"Invalid register code: {code}" );
		}
	}

	#endregion

	#region Win32 API Emulation

	/// <summary>
	/// Initialize Win32 API functions
	/// </summary>
	private void InitializeWin32Api()
	{
		// MessageBoxA - This needs to handle the calling convention properly
		_win32Api["MessageBoxA"] = args =>
		{
			// In the StdCall calling convention (which MessageBoxA uses), parameters are pushed right-to-left
			// So we pop them in reverse: style, title, text, hwnd
			uint style = Pop();
			uint hwnd = Pop();
			uint textPtr = Pop();
			uint titlePtrOrHwnd = Pop();

			// Extra debugging information
			Debug( $"MessageBoxA raw parameters: hwnd=0x{hwnd:X8}, text=0x{textPtr:X8}, title=0x{titlePtrOrHwnd:X8}, style=0x{style:X8}" );

			string text = ReadString( textPtr );
			string title = ReadString( titlePtrOrHwnd );

			Debug( $"MessageBoxA: hwnd=\"{hwnd}\", title=\"{title}\", text=\"{text}\", style={style}" );

			// Call the message box callback if registered
			OnMessageBox?.Invoke( title, text, style );

			// MessageBox returns an integer based on which button was clicked
			// We'll return IDOK (1) for simplicity
			return 1;
		};

		// ExitProcess
		_win32Api["ExitProcess"] = args =>
		{
			uint exitCode = Pop();
			Debug( $"ExitProcess called with code {exitCode}" );

			// Halt execution
			throw new Exception( $"Program terminated with exit code {exitCode}" );
		};

		// GetVersion API - Returns Windows version information
		_win32Api["GetVersion"] = args =>
		{
			// Return Windows 98 version (4.10)
			// Format: The low-order byte contains the major version (4)
			// The second byte contains the minor version (10)
			// The high-order bit is set for Win95/98
			uint win98Version = 0x80000A04; // Windows 98 (4.10)
			Debug( $"GetVersion called, returning Windows 98 (4.10) as 0x{win98Version:X8}" );
			return win98Version;
		};

		// GetVersionEx API - Extended version info
		_win32Api["GetVersionExA"] = args =>
		{
			// Pop the pointer to OSVERSIONINFO structure
			uint pVersionInfo = Pop();

			// Write version info to the structure
			// OSVERSIONINFO.dwOSVersionInfoSize (offset 0)
			uint structSize = ReadDword( pVersionInfo );
			// OSVERSIONINFO.dwMajorVersion (offset 4)
			WriteDword( pVersionInfo + 4, 4 );
			// OSVERSIONINFO.dwMinorVersion (offset 8)
			WriteDword( pVersionInfo + 8, 10 );
			// OSVERSIONINFO.dwBuildNumber (offset 12)
			WriteDword( pVersionInfo + 12, 2222 );
			// OSVERSIONINFO.dwPlatformId (offset 16) - VER_PLATFORM_WIN32_WINDOWS (1)
			WriteDword( pVersionInfo + 16, 1 );

			// OSVERSIONINFO.szCSDVersion (offset 20) - "Service Pack 1"
			byte[] csdVersion = Encoding.ASCII.GetBytes( "Service Pack 1" );
			for ( int i = 0; i < csdVersion.Length; i++ )
			{
				WriteByte( pVersionInfo + 20 + (uint)i, csdVersion[i] );
			}
			WriteByte( pVersionInfo + 20 + (uint)csdVersion.Length, 0 ); // Null terminator

			Debug( "GetVersionExA called, returning Windows 98 SP1" );
			return 1; // TRUE
		};

		_win32Api["GetComputerNameA"] = args =>
		{
			// Pop parameters: lpBuffer and lpnSize
			uint lpnSize = Pop();
			uint lpBuffer = Pop();

			// Our fake computer name
			string computerName = "WIN98-VIRTUAL";

			// First write the name length to lpnSize
			uint nameLength = (uint)computerName.Length;
			WriteDword( lpnSize, nameLength );

			// Then write the computer name to lpBuffer
			for ( int i = 0; i < computerName.Length; i++ )
			{
				WriteByte( lpBuffer + (uint)i, (byte)computerName[i] );
			}
			WriteByte( lpBuffer + (uint)computerName.Length, 0 ); // Null terminator

			Debug( $"GetComputerNameA called, returning '{computerName}'" );
			return 1; // TRUE
		};

		_win32Api["GetSystemMetrics"] = args =>
		{
			// Pop the index parameter
			uint index = Pop();

			// Define some common system metrics
			Dictionary<uint, int> metrics = new Dictionary<uint, int>
			{
				{ 0, 640 },    // SM_CXSCREEN - Screen width
				{ 1, 480 },    // SM_CYSCREEN - Screen height
				{ 16, 0 },     // SM_CXFULLSCREEN - Width available for apps
				{ 17, 0 },     // SM_CYFULLSCREEN - Height available for apps
				{ 30, 1 },     // SM_DBCSENABLED - DBCS enabled
				{ 86, 1 },     // SM_SECURE - Security present (fake)
				{ 80, 0 }      // SM_REMOTESESSION - Not remote
				// Add more as needed
			};

			// Calculate fullscreen sizes (screen minus window borders)
			metrics[16] = metrics[0] - 16;
			metrics[17] = metrics[1] - 38; // Typical Win98 window border heights

			// Return the requested metric
			if ( metrics.TryGetValue( index, out int value ) )
			{
				Debug( $"GetSystemMetrics({index}) called, returning {value}" );
				return (uint)value;
			}

			// Default return value for unimplemented metrics
			Debug( $"GetSystemMetrics({index}) called, returning default 0" );
			return 0;
		};


		// CreateWindowExA implementation
		_win32Api["CreateWindowExA"] = args =>
		{
			// Parameters in reverse order (StdCall convention)
			uint lParam = Pop();           // Additional data
			uint wParam = Pop();           // Additional data
			uint hMenu = Pop();            // Menu handle
			uint hParent = Pop();          // Parent window
			int height = (int)Pop();       // Window height
			int width = (int)Pop();        // Window width
			int y = (int)Pop();            // Y position
			int x = (int)Pop();            // X position
			uint style = Pop();            // Window style
			uint windowName = Pop();       // Window title
			uint className = Pop();        // Window class
			uint extStyle = Pop();         // Extended style

			string title = ReadString( windowName );
			string classNameStr = ReadString( className );

			Debug( $"CreateWindowExA: class=\"{classNameStr}\", title=\"{title}\", pos=({x},{y}), size=({width},{height})" );

			// Create the window using XGUI system if callback is registered
			XGUIPanel windowPanel = null;
			if ( OnCreateWindow != null )
			{
				windowPanel = OnCreateWindow( classNameStr, title, x, y, width, height, style );
			}

			// Generate a window handle
			uint hwnd = _nextWindowHandle++;

			// Store the mapping between handle and panel
			if ( windowPanel != null )
			{
				_windowHandles[hwnd] = windowPanel;
			}

			return hwnd;
		};

		// DestroyWindow
		_win32Api["DestroyWindow"] = args =>
		{
			uint hwnd = Pop();

			// Find and remove the window
			if ( _windowHandles.TryGetValue( hwnd, out var panel ) )
			{
				panel.Delete();
				_windowHandles.Remove( hwnd );
				Debug( $"DestroyWindow: Destroyed window handle 0x{hwnd:X8}" );
				return 1; // TRUE
			}

			Debug( $"DestroyWindow: Window handle 0x{hwnd:X8} not found" );
			return 0; // FALSE
		};

		// ShowWindow
		_win32Api["ShowWindow"] = args =>
		{
			uint cmdShow = Pop();
			uint hwnd = Pop();

			if ( _windowHandles.TryGetValue( hwnd, out var panel ) )
			{
				// Handle show commands (SW_SHOW, SW_HIDE, etc.)
				switch ( cmdShow )
				{
					case 0: // SW_HIDE
						panel.Style.Display = Sandbox.UI.DisplayMode.None;
						break;
					case 5: // SW_SHOW
					case 1: // SW_NORMAL
						panel.Style.Display = Sandbox.UI.DisplayMode.Flex;
						break;
				}

				Debug( $"ShowWindow: Window 0x{hwnd:X8}, command={cmdShow}" );
				return 1;
			}

			Debug( $"ShowWindow: Window handle 0x{hwnd:X8} not found" );
			return 0;
		};

		// RegisterClassA
		_win32Api["RegisterClassA"] = args =>
		{
			// Get pointer to WNDCLASSA structure
			uint lpWndClass = Pop();

			// Read key fields from WNDCLASSA structure
			// uint style = ReadDword(lpWndClass);
			uint hInstance = ReadDword( lpWndClass + 4 );
			uint lpfnWndProc = ReadDword( lpWndClass + 8 );
			uint hIcon = ReadDword( lpWndClass + 16 );
			uint hCursor = ReadDword( lpWndClass + 20 );
			uint lpszClassName = ReadDword( lpWndClass + 36 );

			string className = ReadString( lpszClassName );

			// Register the class
			uint classAtom = (uint)(0x8000 + _windowClasses.Count);
			_windowClasses[className] = classAtom;

			Debug( $"RegisterClassA: Registered class \"{className}\" with atom 0x{classAtom:X4}" );
			return classAtom;
		};

		// DefWindowProcA - Default window procedure
		_win32Api["DefWindowProcA"] = args =>
		{
			uint lParam = Pop();
			uint wParam = Pop();
			uint msg = Pop();
			uint hwnd = Pop();

			// Standard default window proc behavior
			switch ( msg )
			{
				case 0x0002: // WM_DESTROY
							 // Post quit message
					Debug( $"DefWindowProcA: WM_DESTROY for window 0x{hwnd:X8}" );
					return 0;

				case 0x0010: // WM_CLOSE
					Debug( $"DefWindowProcA: WM_CLOSE for window 0x{hwnd:X8}" );
					return 0;
			}

			return 0;
		};

		// LoadIconA
		_win32Api["LoadIconA"] = args =>
		{
			uint iconName = Pop();
			uint hInstance = Pop();

			// Check for predefined icons
			if ( iconName <= 32512 ) // IDI_APPLICATION or other stock icons
			{
				Debug( $"LoadIconA: Loaded stock icon ID={iconName}" );
				return iconName;
			}

			string iconNameStr = ReadString( iconName );
			Debug( $"LoadIconA: Loaded icon \"{iconNameStr}\"" );
			return 0x10000; // Dummy icon handle
		};

		// LoadCursorA
		_win32Api["LoadCursorA"] = args =>
		{
			uint cursorName = Pop();
			uint hInstance = Pop();

			// Check for predefined cursors
			if ( cursorName <= 32512 ) // IDC_ARROW or other stock cursors
			{
				Debug( $"LoadCursorA: Loaded stock cursor ID={cursorName}" );
				return cursorName;
			}

			string cursorNameStr = ReadString( cursorName );
			Debug( $"LoadCursorA: Loaded cursor \"{cursorNameStr}\"" );
			return 0x10000; // Dummy cursor handle
		};

		// GetMessage - Simplified implementation
		_win32Api["GetMessageA"] = args =>
		{
			uint wMsgFilterMax = Pop();
			uint wMsgFilterMin = Pop();
			uint hWnd = Pop();
			uint lpMsg = Pop();

			// In a real implementation, this would wait for a message
			// For now, just return 1 (success) and fake a message
			WriteDword( lpMsg, hWnd );         // hwnd
			WriteDword( lpMsg + 4, 0x0400 );   // WM_USER (generic message)
			WriteDword( lpMsg + 8, 0 );        // wParam
			WriteDword( lpMsg + 12, 0 );       // lParam

			Debug( "GetMessageA: Returned a message" );
			return 1;
		};

		// DispatchMessageA - Simplified implementation
		_win32Api["DispatchMessageA"] = args =>
		{
			uint lpMsg = Pop();

			uint hwnd = ReadDword( lpMsg );
			uint msg = ReadDword( lpMsg + 4 );
			uint wParam = ReadDword( lpMsg + 8 );
			uint lParam = ReadDword( lpMsg + 12 );

			Debug( $"DispatchMessageA: hwnd=0x{hwnd:X8}, msg=0x{msg:X4}" );

			// In a real implementation, this would call the window procedure
			// For now, just return 0
			return 0;
		};

		// Additional APIs needed for basic Win32 GUI
		_win32Api["UpdateWindow"] = args => { uint hwnd = Pop(); return 1; };
		_win32Api["TranslateMessage"] = args => { uint lpMsg = Pop(); return 1; };
		_win32Api["PostQuitMessage"] = args => { int exitCode = (int)Pop(); return 0; };
		_win32Api["BeginPaint"] = args => { uint lpPaint = Pop(); uint hwnd = Pop(); return 0x20000; };
		_win32Api["EndPaint"] = args => { uint lpPaint = Pop(); uint hwnd = Pop(); return 1; };

		// KERNEL32.dll
		_win32Api["GetModuleHandleA"] = args =>
		{
			// Return a fake module handle (nonzero)
			Debug( "GetModuleHandleA called" );
			return 0x10000;
		};

		_win32Api["GetDateFormatA"] = args =>
		{
			// BOOL GetDateFormatA(LCID, DWORD, SYSTEMTIME*, LPCSTR, LPSTR, int)
			uint cchDate = Pop();
			uint lpDateStr = Pop();
			uint lpFormat = Pop();
			uint lpSystemTime = Pop();
			uint dwFlags = Pop();
			uint Locale = Pop();
			Debug( "GetDateFormatA called (stub)" );
			// Write a fake date string
			string date = "01/01/2000";
			for ( int i = 0; i < date.Length && i < cchDate - 1; i++ )
				WriteByte( lpDateStr + (uint)i, (byte)date[i] );
			WriteByte( lpDateStr + (uint)Math.Min( date.Length, cchDate - 1 ), 0 );
			return (uint)date.Length;
		};

		_win32Api["FileTimeToSystemTime"] = args =>
		{
			// BOOL FileTimeToSystemTime(const FILETIME*, LPSYSTEMTIME)
			uint lpSystemTime = Pop();
			uint lpFileTime = Pop();
			Debug( "FileTimeToSystemTime called (stub)" );
			// Write a fake SYSTEMTIME (all zeros)
			for ( int i = 0; i < 16; i++ )
				WriteWord( lpSystemTime + (uint)(i * 2), 0 );
			return 1;
		};

		_win32Api["FileTimeToLocalFileTime"] = args =>
		{
			// BOOL FileTimeToLocalFileTime(const FILETIME*, LPFILETIME)
			uint lpLocalFileTime = Pop();
			uint lpFileTime = Pop();
			Debug( "FileTimeToLocalFileTime called (stub)" );
			// Just copy input to output
			for ( int i = 0; i < 8; i++ )
				WriteByte( lpLocalFileTime + (uint)i, ReadByte( lpFileTime + (uint)i ) );
			return 1;
		};

		_win32Api["lstrcatA"] = args =>
		{
			// LPSTR lstrcatA(LPSTR, LPCSTR)
			uint lpString2 = Pop();
			uint lpString1 = Pop();
			Debug( "lstrcatA called" );
			string s1 = ReadString( lpString1 );
			string s2 = ReadString( lpString2 );
			string result = s1 + s2;
			for ( int i = 0; i < result.Length; i++ )
				WriteByte( lpString1 + (uint)i, (byte)result[i] );
			WriteByte( lpString1 + (uint)result.Length, 0 );
			return lpString1;
		};

		_win32Api["GetTimeFormatA"] = args =>
		{
			// BOOL GetTimeFormatA(LCID, DWORD, SYSTEMTIME*, LPCSTR, LPSTR, int)
			uint cchTime = Pop();
			uint lpTimeStr = Pop();
			uint lpFormat = Pop();
			uint lpSystemTime = Pop();
			uint dwFlags = Pop();
			uint Locale = Pop();
			Debug( "GetTimeFormatA called (stub)" );
			// Write a fake time string
			string time = "12:00";
			for ( int i = 0; i < time.Length && i < cchTime - 1; i++ )
				WriteByte( lpTimeStr + (uint)i, (byte)time[i] );
			WriteByte( lpTimeStr + (uint)Math.Min( time.Length, cchTime - 1 ), 0 );
			return (uint)time.Length;
		};

		// USER32.dll
		_win32Api["LoadStringA"] = args =>
		{
			// int LoadStringA(HINSTANCE, UINT, LPSTR, int)
			uint cchBufferMax = Pop();
			uint lpBuffer = Pop();
			uint uID = Pop();
			uint hInstance = Pop();
			Debug( $"LoadStringA called (stub) for uID={uID}" );
			// Write a fake string
			string str = "WinVer";
			for ( int i = 0; i < str.Length && i < cchBufferMax - 1; i++ )
				WriteByte( lpBuffer + (uint)i, (byte)str[i] );
			WriteByte( lpBuffer + (uint)Math.Min( str.Length, cchBufferMax - 1 ), 0 );
			return (uint)str.Length;
		};

		// SHELL32.dll
		_win32Api["ShellAboutA"] = args =>
		{
			// int ShellAboutA(HWND, LPCSTR, LPCSTR, HICON)
			uint hIcon = Pop();
			uint szOtherStuff = Pop();
			uint szApp = Pop();
			uint hWnd = Pop();
			string app = ReadString( szApp );
			string other = ReadString( szOtherStuff );
			Debug( $"ShellAboutA called: {app} - {other}" );
			OnMessageBox?.Invoke( app, other, 0 );
			return 1;
		};
	}

	/// <summary>
	/// Handle a Win32 API call
	/// </summary>
	private void HandleApiCall( uint apiAddress, int instructionLength = 2 )
	{
		string apiName = _imports.FirstOrDefault( x => x.Value == apiAddress ).Key;

		if ( apiName != null && _win32Api.TryGetValue( apiName, out var handler ) )
		{
			Debug( $"Calling API: {apiName}" );
			uint result = handler( null );
			_registers["eax"] = result;
			_registers["eip"] = Pop();
		}
		else
		{
			string msg = $"Unimplemented API call at address 0x{apiAddress:X8}";
			Debug( msg );
			OnMessageBox?.Invoke(
				"Unimplemented API",
				$"The API function at address 0x{apiAddress:X8} is not implemented.\n" +
				(apiName != null ? $"Function name: {apiName}" : "Function name: Unknown"),
				0 );
			_registers["eax"] = 0; // Set a default return value
			_registers["eip"] += (uint)instructionLength;
		}
	}


	#endregion

	#region Utility Methods

	/// <summary>
	/// Output debug information
	/// </summary>
	private void Debug( string message )
	{
		DebugOutput?.Invoke( message );
		Log.Info( $"X86Interpreter: {message}" );
	}

	private void Warn( string message )
	{
		DebugOutput?.Invoke( message );
		Log.Warning( $"X86Interpreter: {message}" );
	}

	private void Error( string message )
	{
		DebugOutput?.Invoke( message );
		Log.Error( $"X86Interpreter: {message}" );
	}
	private void HaltWithMessageBox( string title, string message )
	{
		// Show a Windows 98 style error message
		MessageBoxUtility.ShowCustom( message, title, MessageBoxIcon.Error, MessageBoxButtons.OK );
		throw new Exception( $"{title}: {message}" );
	}

	/// <summary>
	/// Dump register state
	/// </summary>
	public string DumpRegisters()
	{
		var sb = new StringBuilder();
		sb.AppendLine( "Register state:" );

		foreach ( var reg in _registers )
		{
			sb.AppendLine( $"{reg.Key} = 0x{reg.Value:X8}" );
		}

		return sb.ToString();
	}

	#endregion

	#region Misc and unsorted APIs
	private static void ParseMessageBoxStyle( uint style, out MessageBoxIcon icon, out MessageBoxButtons buttons )
	{
		// Default values
		icon = MessageBoxIcon.None;
		buttons = MessageBoxButtons.OK;

		// Icon flags (see WinUser.h)
		if ( (style & 0x10) != 0 ) icon = MessageBoxIcon.Error;        // MB_ICONHAND/MB_ICONERROR
		else if ( (style & 0x20) != 0 ) icon = MessageBoxIcon.Question; // MB_ICONQUESTION
		else if ( (style & 0x30) != 0 ) icon = MessageBoxIcon.Warning;  // MB_ICONEXCLAMATION/MB_ICONWARNING
		else if ( (style & 0x40) != 0 ) icon = MessageBoxIcon.Information; // MB_ICONASTERISK/MB_ICONINFORMATION

		// Button flags
		switch ( style & 0xF )
		{
			case 0x0: buttons = MessageBoxButtons.OK; break;              // MB_OK
			case 0x1: buttons = MessageBoxButtons.OKCancel; break;        // MB_OKCANCEL
			case 0x2: buttons = MessageBoxButtons.AbortRetryIgnore; break;// MB_ABORTRETRYIGNORE
			case 0x3: buttons = MessageBoxButtons.YesNoCancel; break;     // MB_YESNOCANCEL
			case 0x4: buttons = MessageBoxButtons.OK; break;           // MB_YESNO
			case 0x5: buttons = MessageBoxButtons.RetryCancel; break;     // MB_RETRYCANCEL
																		  // Add more if your MessageBox implementation supports them
			default: buttons = MessageBoxButtons.OK; break;
		}
	}
	#endregion

	#region Console Command

	/// <summary>
	/// Console command to run a Win32 executable from the virtual file system
	/// </summary>

	[ConCmd( "xguitest_run_x86_exec" )]
	public static void RunX86Exec( string path )
	{
		Log.Info( $"Running x86 executable from: {path}" );

		try
		{
			// Read the file from the virtual file system
			byte[] fileBytes = FileSystem.Data.ReadAllBytes( path ).ToArray();

			if ( fileBytes.Length == 0 )
			{
				Log.Error( $"Failed to read executable file: {path}" );
				return;
			}

			// Create the interpreter
			var interpreter = new X86Interpreter();

			// Register debug output
			interpreter.DebugOutput = message => Log.Info( $"X86: {message}" );

			// Register MessageBox handler with fancy display
			interpreter.OnMessageBox = ( title, text, style ) =>
			{
				string displayTitle = string.IsNullOrEmpty( title ) ? "Message" : title;

				// Parse style
				ParseMessageBoxStyle( style, out var icon, out var buttons );

				// Display a fancy message box in the console (unchanged)
				Log.Info( "" );
				Log.Info( "╔" + new string( '═', Math.Min( 70, displayTitle.Length + 4 ) ) + "╗" );
				Log.Info( $"║  {displayTitle}  ║" );
				Log.Info( "╠" + new string( '═', Math.Min( 70, displayTitle.Length + 4 ) ) + "╣" );
				int boxWidth = Math.Max( displayTitle.Length + 4, 40 );
				string[] lines = text.Split( '\n' );
				foreach ( var line in lines )
				{
					string trimmedLine = line.TrimEnd();
					int padding = Math.Max( 0, boxWidth - trimmedLine.Length - 4 );
					Log.Info( $"║  {trimmedLine}" + new string( ' ', padding ) + "  ║" );
				}
				Log.Info( "╚" + new string( '═', Math.Min( 70, boxWidth ) ) + "╝" );
				Log.Info( "" );

				// Show the actual message box
				MessageBoxUtility.ShowCustom( text, displayTitle, icon, buttons );
			};

			// Register CreateWindow handler
			interpreter.OnCreateWindow = ( className, title, x, y, width, height, style ) =>
			{
				// Create a window using XGUI
				var windowPanel = new Window();
				windowPanel.AddClass( "window" );
				windowPanel.Size.x = width;
				windowPanel.Size.y = height;
				windowPanel.Position.x = x;
				windowPanel.Position.y = y;
				windowPanel.Title = title;

				// Add to XGUI system
				XGUISystem.Instance.Panel.AddChild( windowPanel );

				Log.Info( $"Created window: {title} ({width}x{height} at {x},{y})" );

				return windowPanel;
			};

			// Load and parse the executable
			if ( interpreter.LoadExecutable( fileBytes ) )
			{
				// Find and register API functions
				interpreter.FindAndRegisterApiFunction( fileBytes, "CreateWindowExA", 0xFFFF0002 );
				interpreter.FindAndRegisterApiFunction( fileBytes, "RegisterClassA", 0xFFFF0003 );
				interpreter.FindAndRegisterApiFunction( fileBytes, "ShowWindow", 0xFFFF0004 );
				interpreter.FindAndRegisterApiFunction( fileBytes, "DefWindowProcA", 0xFFFF0005 );
				interpreter.FindAndRegisterApiFunction( fileBytes, "LoadIconA", 0xFFFF0006 );
				interpreter.FindAndRegisterApiFunction( fileBytes, "LoadCursorA", 0xFFFF0007 );

				// Fix the import table address to point to our MessageBoxA function
				interpreter.WriteDword( 0x00402050, 0xFFFF0001 );

				// Execute the program
				bool success = interpreter.Execute();

				if ( success )
				{
					// Output final register state
					Log.Info( interpreter.DumpRegisters() );
					Log.Info( "Execution completed successfully!" );
				}
			}
			else
			{
				Log.Error( "Failed to load executable." );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error executing x86 program: {ex.Message}" );
		}
	}


	/// <summary>
	/// Console command to create a test executable
	/// </summary>
	[ConCmd( "xguitest_create_test_exe" )]
	public static void CreateTestExe( string path = "FakeSystemRoot/test_messagebox.exe" )
	{
		// This loads our mini MessageBox executable raw bytes
		var exeBytes = FakeDesktop.FakeExecutable.ExeTemplateBytes;

		// Write it to the specified path
		var stream = FileSystem.Data.OpenWrite( path );
		stream.Write( exeBytes, 0, exeBytes.Length );

		Log.Info( $"Created test executable at {path}" );
		Log.Info( $"You can now run it with: xguitest_run_x86_exec {path}" );
	}

	#endregion
}

