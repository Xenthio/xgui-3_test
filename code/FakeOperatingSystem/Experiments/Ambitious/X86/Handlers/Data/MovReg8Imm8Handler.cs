namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0xB0-0xB7 — MOV r8, imm8
public class MovReg8Imm8Handler : IInstructionHandler
{
	private static readonly string[] Reg8Names = { "eax", "ecx", "edx", "ebx", "eax", "ecx", "edx", "ebx" };
	// B0=AL B1=CL B2=DL B3=BL B4=AH B5=CH B6=DH B7=BH
	// High-byte flag: B4-B7 write bits 8-15
	private static readonly bool[] IsHigh = { false, false, false, false, true, true, true, true };

	public bool CanHandle( byte opcode ) => opcode >= 0xB0 && opcode <= 0xB7;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		int idx = opcode - 0xB0;
		byte imm = core.ReadByte( eip + 1 );
		string reg = Reg8Names[idx];
		if ( IsHigh[idx] )
		{
			core.Registers[reg] = (core.Registers[reg] & 0xFFFF00FF) | ((uint)imm << 8);
		}
		else
		{
			core.Registers[reg] = (core.Registers[reg] & 0xFFFFFF00) | imm;
		}
		core.Registers["eip"] += 2;
	}
}
