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

	public X86Interpreter()
	{
		APIEmulators.Add( new User32Emulator() );

		InstructionSet.RegisterHandler( new Handlers.MovRegImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.PushImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.PushImm8Handler() );

		InstructionSet.RegisterHandler( new Handlers.Opcode00Handler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeFFHandler( this ) );
	}

	public bool LoadExecutable( byte[] fileBytes )
	{
		var loader = new PELoader();
		return loader.Load( fileBytes, Core, out _entryPoint, out Imports );
	}

	public void Execute()
	{
		Core.Registers["eip"] = _entryPoint;
		int maxInstructions = 10000;

		for ( int i = 0; i < maxInstructions; i++ )
		{
			try
			{
				InstructionSet.ExecuteNext( Core );
			}
			catch ( System.Exception ex )
			{
				// Optionally log or handle errors
				Log.Error( $"Execution error: {ex.Message}" );
				//break;
			}
		}
	}
}
