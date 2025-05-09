namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class PopEsHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x07;

	public void Execute( X86Core core )
	{
		// POP ES is a legacy instruction; treat as NOP for protected mode
		core.Registers["eip"] += 1;
	}
}
