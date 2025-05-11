using FakeDesktop;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public partial class X86Interpreter
{
	public readonly X86Core Core = new();
	public readonly X86InstructionSet InstructionSet = new();
	public readonly List<APIEmulator> APIEmulators = new();
	public Dictionary<string, uint> Imports = new();
	private uint _entryPoint;
	public string ExecutableName { get; private set; } = "VIRTUAL.EXE";
	public Dictionary<string, string> ImportSourceDlls = new();

	public uint HeapStart = 0x00400000; // Default heap start address

	public Dictionary<(uint hInstance, uint uID), string> StringResources = new();

	public delegate void MessageBoxHandler( string title, string message, MessageBoxIcon icon = MessageBoxIcon.Error, MessageBoxButtons buttons = MessageBoxButtons.AbortRetryIgnore );
	public event MessageBoxHandler OnHaltWithMessageBox;

	public X86Interpreter()
	{
		APIEmulators.Add( new User32Emulator() );
		APIEmulators.Add( new MsvcrtEmulator() );
		APIEmulators.Add( new Kernel32Emulator() );
		APIEmulators.Add( new Shell32Emulator() );

		// === Miscellaneous ===
		InstructionSet.RegisterHandler( new Handlers.NopHandler() );
		InstructionSet.RegisterHandler( new Handlers.HltHandler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode00Handler() );

		// === Arithmetic ===
		InstructionSet.RegisterHandler( new Handlers.AddRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AddR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.SubRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.SubR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AdcRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.AdcR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AdcRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.XorRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.XorRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.XorHandler() );
		InstructionSet.RegisterHandler( new Handlers.OrR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.OrEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AndR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AndAlImm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.AndEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.BCDArithmeticHandler() );
		InstructionSet.RegisterHandler( new Handlers.CmpHandler() );
		InstructionSet.RegisterHandler( new Handlers.CmpAlImm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode80Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode81Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode83Handler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeF6Handler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeF7Handler() );

		// === Data Movement ===
		InstructionSet.RegisterHandler( new Handlers.MovRegImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovRmR32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovR32RmHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovRm32Imm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovEaxMemHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovR8Rm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovMoffs32EaxHandler() );
		InstructionSet.RegisterHandler( new Handlers.LesHandler() );
		InstructionSet.RegisterHandler( new Handlers.PopRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.PushRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.PushImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.PushImm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.SegmentPrefixHandler() );
		InstructionSet.RegisterHandler( new Handlers.PopEsHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovReg8SSRm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.LeaHandler() );
		InstructionSet.RegisterHandler( new Handlers.XchgHandler() );

		// === Control Flow ===
		InstructionSet.RegisterHandler( new Handlers.CallRel32Handler() );
		InstructionSet.RegisterHandler( new Handlers.JmpHandler() );
		InstructionSet.RegisterHandler( new Handlers.ConditionalJumpHandler() );
		InstructionSet.RegisterHandler( new Handlers.RetHandler( this ) );
		InstructionSet.RegisterHandler( new Handlers.LoopHandler() );
		InstructionSet.RegisterHandler( new Handlers.ExtendedOpcodeHandler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeFFHandler( this ) );

		// === Logic/Bitwise ===
		InstructionSet.RegisterHandler( new Handlers.TestRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.TestRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.TestEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.FlagInstructionHandler() );
		InstructionSet.RegisterHandler( new Handlers.ShiftRotateHandler() );
		InstructionSet.RegisterHandler( new Handlers.OperandSizePrefixHandler() );

		// === Stack/Frame ===
		InstructionSet.RegisterHandler( new Handlers.LeaveHandler() );
		InstructionSet.RegisterHandler( new Handlers.IncDecRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.CdqHandler() );

		// === String/Memory ===
		InstructionSet.RegisterHandler( new Handlers.StringOperationsHandler() );

		// === Port/IO ===
		InstructionSet.RegisterHandler( new Handlers.PortIOHandler() );

		// === Testing ===

		//InstructionSet.RegisterHandler( new Handlers.TestingHandlerNotReal() );
	}

	public bool LoadExecutable( byte[] fileBytes, string path = null )
	{
		// Extract exe name from path if available
		if ( !string.IsNullOrEmpty( path ) )
		{
			ExecutableName = System.IO.Path.GetFileName( path ).ToUpper();
		}

		var loader = new PELoader();
		bool loaded = loader.Load( fileBytes, Core, out _entryPoint, out Imports, out ImportSourceDlls, out HeapStart );

		if ( loader.ParseAllResources( fileBytes, out var resources ) )
		{
			foreach ( var res in resources )
			{
				if ( res.Type == 6 ) // RT_STRING
				{
					using var ms = new System.IO.MemoryStream( res.Data );
					using var br = new System.IO.BinaryReader( ms );
					for ( uint i = 0; i < 16; i++ )
					{
						if ( ms.Position + 2 > ms.Length )
							break; // Prevents reading past end

						ushort strlen = br.ReadUInt16();
						string value = "";
						if ( strlen > 0 )
						{
							if ( ms.Position + strlen * 2 > ms.Length )
								break; // Prevents reading past end

							byte[] strBytes = br.ReadBytes( strlen * 2 );
							value = Encoding.Unicode.GetString( strBytes );
							// Only add if not empty
							StringResources[(0x00400000, (res.Name - 1) * 16 + i)] = value;
							Core.LogVerbose( $"Loaded string resource: ID=0x{((res.Name - 1) * 16 + i):X8}, Value=\"{value}\"" );
						}
					}
				}
			}
		}

		return loaded;
	}

	[ConVar( "xguitest_x86_log_eip" )]
	public static bool EIPLogging { get; set; } = false;
	private bool _haltASAP = false;
	public async Task ExecuteAsync( uint maxInstructions = 0xFFFFFFFF, int yieldEvery = 100 )
	{
		Core.Push( 0xFFFFFFFF ); // Address of our final return, this will be used if we hit a RET without anything else in the stack, which we can assume is our final RET
		Core.Registers["eip"] = _entryPoint;
		int i = 0;

		for ( i = 0; i < maxInstructions; i++ )
		{
			// Yield to UI every N instructions 
			// Check for program exit
			if ( Core.Registers["eip"] == 0xFFFFFFFF )
			{
				Log.Info( "Program execution completed via final RET" );
				break;
			}

			if ( _haltASAP )
			{
				Log.Info( "Execution halted by user request." );
				break;
			}

			if ( EIPLogging )
			{
				Log.Info( $"EIP: 0x{Core.Registers["eip"]:X8}" );
			}

			try
			{
				InstructionSet.ExecuteNext( Core, this );
			}
			catch ( System.Exception ex )
			{
				Log.Error( $"Execution error at EIP 0x{Core.Registers["eip"]:X8}: {ex.Message}" );

				if ( !ex.Message.StartsWith( "!" ) )
				{
					MessageBoxUtility.ShowCustom(
						$"Execution error at 0x{Core.Registers["eip"]:X8}: {ex.Message}",
						"Execution Error",
						MessageBoxIcon.Error,
						MessageBoxButtons.OK
					);
				}
				break;
			}

			// Yield to UI every N instructions
			if ( (i % yieldEvery) == 0 )
				await Task.Yield();

			// Every 100000, log EIP and executable name
			if ( (i % 100000) == 0 )
			{
				Log.Info( $"EIP: 0x{Core.Registers["eip"]:X8} - Still executing {ExecutableName}!" );
			}
		}

		if ( i >= maxInstructions )
		{
			Log.Warning( "Execution reached maximum instruction limit." );
		}
		else
		{
			Log.Info( $"Executed {i} instructions." );
		}
		OnFinish?.Invoke();
	}

	public void Halt()
	{
		_haltASAP = true;
	}

	public Action OnFinish;

	public void DumpMemory( uint start, uint length )
	{
		var memdump = "";
		for ( uint i = 0; i < length; i++ )
		{
			byte b = Core.ReadByte( start + i );
			memdump += $"{b:X2} ";
			if ( (i + 1) % 16 == 0 )
			{
				memdump += "\n";
			}
		}
		Log.Info( memdump );
	}

	public void DumpMemoryAsString( uint start, uint length )
	{
		var memdump = "";
		for ( uint i = 0; i < length; i++ )
		{
			byte b = Core.ReadByte( start + i );
			if ( b >= 32 && b <= 126 ) // Printable ASCII range
			{
				memdump += (char)b;
			}
			else
			{
				memdump += ".";
			}
			if ( (i + 1) % 16 == 0 )
			{
				memdump += "\n";
			}
		}
		Log.Info( memdump );
	}

	public string DumpRegisters()
	{
		var dump = new System.Text.StringBuilder();
		dump.AppendLine( "=== Register Values ===" );
		dump.AppendLine( $"EAX: 0x{Core.Registers["eax"]:X8}" );
		dump.AppendLine( $"EBX: 0x{Core.Registers["ebx"]:X8}" );
		dump.AppendLine( $"ECX: 0x{Core.Registers["ecx"]:X8}" );
		dump.AppendLine( $"EDX: 0x{Core.Registers["edx"]:X8}" );
		dump.AppendLine( $"ESI: 0x{Core.Registers["esi"]:X8}" );
		dump.AppendLine( $"EDI: 0x{Core.Registers["edi"]:X8}" );
		dump.AppendLine( $"EBP: 0x{Core.Registers["ebp"]:X8}" );
		dump.AppendLine( $"ESP: 0x{Core.Registers["esp"]:X8}" );
		dump.AppendLine( $"EIP: 0x{Core.Registers["eip"]:X8}" );
		dump.AppendLine( "=== Flags ===" );
		dump.AppendLine( $"ZF: {Core.ZeroFlag}, SF: {Core.SignFlag}, CF: {Core.CarryFlag}, OF: {Core.OverflowFlag}" );
		return dump.ToString();
	}

	public void HaltWithMessageBox( string title, string message, MessageBoxIcon icon = MessageBoxIcon.Error, MessageBoxButtons buttons = MessageBoxButtons.AbortRetryIgnore )
	{
		OnHaltWithMessageBox?.Invoke( title, message, icon, buttons );
		throw new System.Exception( $"!{title}: {message}" );
	}
}
