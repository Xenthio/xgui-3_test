namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0xC6 /0 — MOV r/m8, imm8
public class MovRm8Imm8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xC6;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte rm  = (byte)(modrm & 0x7);

		if ( mod == 3 )
		{
			// MOV r8, imm8 — imm8 follows the modrm byte
			byte imm = core.ReadByte( eip + 2 );
			string reg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[reg] = (core.Registers[reg] & 0xFFFFFF00) | imm;
			core.Registers["eip"] += 3;
		}
		else
		{
			// MOV [mem], imm8
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint len  = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			// imm8 is at eip + len (after the modrm/sib/disp bytes)
			byte imm = core.ReadByte( eip + len );
			core.WriteByte( addr, imm );
			core.Registers["eip"] += len + 1;
		}
	}
}
