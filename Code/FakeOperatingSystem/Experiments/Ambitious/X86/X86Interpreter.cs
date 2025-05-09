using FakeDesktop;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using System.Collections.Generic;
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

	public delegate void MessageBoxHandler( string title, string message, MessageBoxIcon icon = MessageBoxIcon.Error, MessageBoxButtons buttons = MessageBoxButtons.AbortRetryIgnore );
	public event MessageBoxHandler OnHaltWithMessageBox;

	public X86Interpreter()
	{
		APIEmulators.Add( new User32Emulator() );
		APIEmulators.Add( new MsvcrtEmulator() );
		APIEmulators.Add( new Kernel32Emulator() );
		APIEmulators.Add( new Shell32Emulator() );

		// Register all instruction handlers
		InstructionSet.RegisterHandler( new Handlers.AddRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AddR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovRegImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.PushImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.PushImm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.CallRel32Handler() );
		InstructionSet.RegisterHandler( new Handlers.RetHandler() );
		InstructionSet.RegisterHandler( new Handlers.JmpHandler() );
		InstructionSet.RegisterHandler( new Handlers.ConditionalJumpHandler() );
		InstructionSet.RegisterHandler( new Handlers.LeaveHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovRmR32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovR32RmHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovRm32Imm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.XorRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.IncDecRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.PushRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.CmpHandler() );
		InstructionSet.RegisterHandler( new Handlers.LeaHandler() );
		InstructionSet.RegisterHandler( new Handlers.PopRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.NopHandler() );
		InstructionSet.RegisterHandler( new Handlers.XorHandler() );
		InstructionSet.RegisterHandler( new Handlers.SegmentPrefixHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovEaxMemHandler() );
		InstructionSet.RegisterHandler( new Handlers.OrEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.FlagInstructionHandler() );
		InstructionSet.RegisterHandler( new Handlers.ExtendedOpcodeHandler() );
		InstructionSet.RegisterHandler( new Handlers.ShiftRotateHandler() );
		InstructionSet.RegisterHandler( new Handlers.OperandSizePrefixHandler() );
		InstructionSet.RegisterHandler( new Handlers.TestRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.HltHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovR8Rm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.SubRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.SubR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AdcRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.CdqHandler() );
		InstructionSet.RegisterHandler( new Handlers.PortIOHandler() );
		InstructionSet.RegisterHandler( new Handlers.LesHandler() );
		InstructionSet.RegisterHandler( new Handlers.BCDArithmeticHandler() );
		InstructionSet.RegisterHandler( new Handlers.AndAlImm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.XchgHandler() );
		InstructionSet.RegisterHandler( new Handlers.LoopHandler() );
		InstructionSet.RegisterHandler( new Handlers.TestEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.StringOperationsHandler() );
		InstructionSet.RegisterHandler( new Handlers.TestRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.XorRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.OrR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.CmpAlImm8Handler() );



		InstructionSet.RegisterHandler( new Handlers.Opcode00Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode80Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode81Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode83Handler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeF6Handler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeFFHandler( this ) );
	}

	public bool LoadExecutable( byte[] fileBytes, string path = null )
	{
		// Extract exe name from path if available
		if ( !string.IsNullOrEmpty( path ) )
		{
			// Get just the filename from the path
			ExecutableName = System.IO.Path.GetFileName( path ).ToUpper();
		}

		var loader = new PELoader();
		return loader.Load( fileBytes, Core, out _entryPoint, out Imports, out ImportSourceDlls );
	}

	public void Execute()
	{
		Core.Push( 0xFFFFFFFF ); // Address of our final return, this will be used if we hit a RET without anything else in the stack, which we can assume is our final RET
		Core.Registers["eip"] = _entryPoint;
		uint maxInstructions = 0xFFFFFFFF;
		var i = 0;

		for ( i = 0; i < maxInstructions; i++ )
		{
			// Check for program exit
			if ( Core.Registers["eip"] == 0xFFFFFFFF )
			{
				Log.Info( "Program execution completed via final RET" );
				break;
			}

			try
			{
				InstructionSet.ExecuteNext( Core, this );
			}
			catch ( System.Exception ex )
			{
				// Optionally log or handle errors
				Log.Error( $"Execution error at 0x{Core.Registers["eip"]:X8}: {ex.Message}" );

				// dont show popup if it starts with ! (Means we've shown a message already)
				if ( !ex.Message.StartsWith( "!" ) )
				{
					MessageBoxUtility.ShowCustom( $"Execution error at 0x{Core.Registers["eip"]:X8}: {ex.Message}", "Execution Error", MessageBoxIcon.Error, MessageBoxButtons.OK );
				}
				break; // Exit on errors
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

		Log.Info( "Execution completed successfully!" );
		DumpMemory( 0x00401000, 0x0300 ); // Example memory dump
		DumpMemoryAsString( 0x00401000, 0x0300 ); // Example memory dump
		DumpMemory( 0x00402000, 0x0300 ); // Example memory dump
		DumpMemoryAsString( 0x00402000, 0x0300 ); // Example memory dump
		DumpMemory( 0x00602000, 0x0300 ); // Example memory dump
		DumpMemoryAsString( 0x00602000, 0x0300 ); // Example memory dump
		Log.Info( DumpRegisters() );
	}

	public async Task ExecuteAsync( uint maxInstructions = 0xFFFFFFFF, int yieldEvery = 10_000 )
	{
		Core.Push( 0xFFFFFFFF ); // Address of our final return, this will be used if we hit a RET without anything else in the stack, which we can assume is our final RET
		Core.Registers["eip"] = _entryPoint;
		int i = 0;

		for ( i = 0; i < maxInstructions; i++ )
		{
			// Check for program exit
			if ( Core.Registers["eip"] == 0xFFFFFFFF )
			{
				Log.Info( "Program execution completed via final RET" );
				break;
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
		}

		if ( i >= maxInstructions )
		{
			Log.Warning( "Execution reached maximum instruction limit." );
		}
		else
		{
			Log.Info( $"Executed {i} instructions." );
		}
	}

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
