namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class TestingHandlerNotReal : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x0c;

	public void Execute( X86Core core )
	{
		Log.Info( "Test to see if 0x0C is executed" );

		// Advance past the instruction
		core.Registers["eip"] += 1;
	}
}

