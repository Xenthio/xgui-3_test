using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// 0x69 — IMUL r32, r/m32, imm32
/// 0x6B — IMUL r32, r/m32, imm8 (sign-extended)
/// Two-operand forms with immediate: dest = src * imm
/// </summary>
public class ImulImmHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x69 || opcode == 0x6B;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7); // destination
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );

		// ── Read source value ─────────────────────────────────────────────────
		int srcVal;
		uint instrBase = eip;
		uint modrmSize;

		if ( mod == 3 )
		{
			srcVal = (int)core.Registers[X86AddressingHelper.GetRegisterName( rm )];
			modrmSize = 2; // opcode + modrm
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, instrBase );
			srcVal = (int)core.ReadDword( addr );
			modrmSize = X86AddressingHelper.GetInstructionLength( modrm, core, instrBase ) - 1 + 1; // modrm+SIB+disp + opcode
		}

		// ── Read immediate ────────────────────────────────────────────────────
		int imm;
		uint immSize;
		if ( opcode == 0x6B )
		{
			imm = (sbyte)core.ReadByte( (uint)(instrBase + modrmSize) );
			immSize = 1;
		}
		else
		{
			imm = (int)core.ReadDword( (uint)(instrBase + modrmSize) );
			immSize = 4;
		}

		// ── Multiply ──────────────────────────────────────────────────────────
		long full = (long)srcVal * imm;
		uint result = (uint)(full & 0xFFFFFFFF);
		core.Registers[destReg] = result;

		// Overflow/Carry set when result doesn't fit in 32 bits
		bool overflow = full != (int)result;
		core.OverflowFlag = overflow;
		core.CarryFlag = overflow;
		// ZF/SF undefined by Intel spec for IMUL but many real CPUs set them:
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;

		core.LogVerbose( $"IMUL {destReg}, src=0x{(uint)srcVal:X8}, imm=0x{(uint)imm:X8} => 0x{result:X8}" );

		core.Registers["eip"] = (uint)(instrBase + modrmSize + immSize);
	}


}
