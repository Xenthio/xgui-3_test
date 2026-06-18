using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0xC7 /0 — MOV r/m32, imm32
public class MovRm32Imm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xC7;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte rm  = (byte)(modrm & 0x7);

		if ( mod == 3 )
		{
			// Register destination: opcode(1) + modrm(1) + imm32(4)
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			uint imm32 = core.ReadDword( eip + 2 );
			core.Registers[destReg] = imm32;
			core.Registers["eip"] += 6;
		}
		else
		{
			// Memory destination — use the shared helper for EA + length
			uint ea  = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			// imm32 immediately follows the modrm/sib/disp bytes
			uint imm32 = core.ReadDword( eip + len );
			core.WriteDword( ea, imm32 );
			// total: 1 (opcode already counted in eip) + len + 4
			core.Registers["eip"] += len + 4;
		}
	}
}
