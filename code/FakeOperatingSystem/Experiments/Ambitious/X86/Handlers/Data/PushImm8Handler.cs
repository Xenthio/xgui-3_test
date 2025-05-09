namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class PushImm8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x6A;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		sbyte imm8 = (sbyte)core.ReadByte( eip + 1 );
		core.Push( (uint)imm8 ); // Sign-extended
		core.Registers["eip"] += 2;
	}
}
