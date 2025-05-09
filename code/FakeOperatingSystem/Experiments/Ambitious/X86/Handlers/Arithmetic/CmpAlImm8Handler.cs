namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class CmpAlImm8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x3C;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte imm8 = core.ReadByte( eip + 1 );
		byte al = (byte)(core.Registers["eax"] & 0xFF);

		int result = al - imm8;

		// Set flags
		core.ZeroFlag = (result & 0xFF) == 0;
		core.SignFlag = (result & 0x80) != 0;
		core.CarryFlag = al < imm8;
		// Overflow: if sign of operands differ and sign of result differs from sign of AL
		core.OverflowFlag = ((al ^ imm8) & (al ^ result) & 0x80) != 0;

		core.Registers["eip"] += 2;
	}
}
