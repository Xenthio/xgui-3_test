namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class Opcode00Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x00;

	public void Execute( X86Core core )
	{
		// Optionally: count consecutive 0x00s and halt if too many
		core.Registers["eip"] += 1;
		// You could also throw or log if you want to treat this as an error
	}
}
