namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public interface IInstructionHandler
{
	bool CanHandle( byte opcode );
	void Execute( X86Core core );
}
