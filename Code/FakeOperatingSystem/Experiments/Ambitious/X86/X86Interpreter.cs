using FakeDesktop;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using System.Collections.Generic;

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

		// Register all instruction handlers
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
		InstructionSet.RegisterHandler( new Handlers.TestHandler() );
		InstructionSet.RegisterHandler( new Handlers.HltHandler() );

		InstructionSet.RegisterHandler( new Handlers.Opcode00Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode81Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode83Handler() );
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
		Core.Registers["eip"] = _entryPoint;
		int maxInstructions = 10000;

		for ( int i = 0; i < maxInstructions; i++ )
		{
			try
			{
				InstructionSet.ExecuteNext( Core, this );
			}
			catch ( System.Exception ex )
			{
				// Optionally log or handle errors
				Log.Error( $"Execution error: {ex.Message}" );
				break; // Exit on errors
			}
		}
	}

	public void HaltWithMessageBox( string title, string message, MessageBoxIcon icon = MessageBoxIcon.Error, MessageBoxButtons buttons = MessageBoxButtons.AbortRetryIgnore )
	{
		OnHaltWithMessageBox?.Invoke( title, message, icon, buttons );
		throw new System.Exception( $"{title}: {message}" );
	}
}
