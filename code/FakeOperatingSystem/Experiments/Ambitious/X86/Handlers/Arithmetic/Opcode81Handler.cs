using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// 0x81 — Immediate Group 1: ADD/OR/ADC/SBB/AND/SUB/XOR/CMP r/m32, imm32
/// Fixed: memory operands now fully supported; all ops set correct flags; EIP advanced correctly.
/// </summary>
public class Opcode81Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x81;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		bool isReg = (mod == 3);

		// ── Read imm32 ────────────────────────────────────────────────────────
		// For mod==3: 1 (opcode) + 1 (modrm) = offset 2
		// For memory: offset is past modrm + SIB + displacement bytes
		uint instrBase = eip;
		uint memAddr = 0;
		uint immOffset;

		if ( isReg )
		{
			immOffset = instrBase + 2;
		}
		else
		{
			memAddr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, instrBase );
			// GetInstructionLength returns total bytes for opcode+modrm+SIB+disp, we need bytes after opcode
			uint modrmSize = X86AddressingHelper.GetInstructionLength( modrm, core, instrBase ) - 1; // -1 because helper includes opcode byte
			immOffset = instrBase + 1 + modrmSize; // opcode(1) + modrm+sib+disp
		}

		uint imm32 = core.ReadDword( immOffset );

		// ── Read operand ─────────────────────────────────────────────────────
		uint value = isReg
			? core.Registers[X86AddressingHelper.GetRegisterName( rm )]
			: core.ReadDword( memAddr );

		// ── Execute operation ─────────────────────────────────────────────────
		uint result = 0;
		bool writeBack = true;

		switch ( reg )
		{
			case 0: // ADD
				result = value + imm32;
				SetFlagsAdd( core, value, imm32, result );
				core.LogVerbose( $"ADD {(isReg ? X86AddressingHelper.GetRegisterName( rm ) : $"[0x{memAddr:X8}]")}, 0x{imm32:X8} => 0x{result:X8}" );
				break;

			case 1: // OR
				result = value | imm32;
				SetFlagsLogic( core, result );
				core.LogVerbose( $"OR {(isReg ? X86AddressingHelper.GetRegisterName( rm ) : $"[0x{memAddr:X8}]")}, 0x{imm32:X8} => 0x{result:X8}" );
				break;

			case 2: // ADC
				{
					uint carry = core.CarryFlag ? 1u : 0u;
					ulong full = (ulong)value + imm32 + carry;
					result = (uint)full;
					core.CarryFlag = full > 0xFFFFFFFF;
					core.ZeroFlag = result == 0;
					core.SignFlag = (result & 0x80000000) != 0;
					bool vs = (value & 0x80000000) != 0;
					bool is_ = (imm32 & 0x80000000) != 0;
					bool rs = (result & 0x80000000) != 0;
					core.OverflowFlag = (vs == is_) && (rs != vs);
				}
				break;

			case 3: // SBB
				{
					uint borrow = core.CarryFlag ? 1u : 0u;
					ulong full = (ulong)value - imm32 - borrow;
					result = (uint)full;
					core.CarryFlag = full > 0xFFFFFFFF;
					core.ZeroFlag = result == 0;
					core.SignFlag = (result & 0x80000000) != 0;
					bool vs = (value & 0x80000000) != 0;
					bool is_ = (imm32 & 0x80000000) != 0;
					bool rs = (result & 0x80000000) != 0;
					core.OverflowFlag = (vs != is_) && (rs != vs);
				}
				break;

			case 4: // AND
				result = value & imm32;
				SetFlagsLogic( core, result );
				core.LogVerbose( $"AND {(isReg ? X86AddressingHelper.GetRegisterName( rm ) : $"[0x{memAddr:X8}]")}, 0x{imm32:X8} => 0x{result:X8}" );
				break;

			case 5: // SUB
				result = value - imm32;
				SetFlagsSub( core, value, imm32, result );
				core.LogVerbose( $"SUB {(isReg ? X86AddressingHelper.GetRegisterName( rm ) : $"[0x{memAddr:X8}]")}, 0x{imm32:X8} => 0x{result:X8}" );
				break;

			case 6: // XOR
				result = value ^ imm32;
				SetFlagsLogic( core, result );
				core.LogVerbose( $"XOR {(isReg ? X86AddressingHelper.GetRegisterName( rm ) : $"[0x{memAddr:X8}]")}, 0x{imm32:X8} => 0x{result:X8}" );
				break;

			case 7: // CMP — sets flags but discards result
				result = value - imm32;
				SetFlagsSub( core, value, imm32, result );
				writeBack = false;
				core.LogVerbose( $"CMP {(isReg ? X86AddressingHelper.GetRegisterName( rm ) : $"[0x{memAddr:X8}]")}, 0x{imm32:X8} (result=0x{result:X8})" );
				break;

			default:
				throw new NotImplementedException( $"0x81 /reg={reg} not defined" );
		}

		// ── Write back ────────────────────────────────────────────────────────
		if ( writeBack )
		{
			if ( isReg )
				core.Registers[X86AddressingHelper.GetRegisterName( rm )] = result;
			else
				core.WriteDword( memAddr, result );
		}

		// ── Advance EIP: opcode(1) + modrm/SIB/disp + imm32(4) ──────────────
		if ( isReg )
		{
			core.Registers["eip"] += 6; // 1 + 1 + 4
		}
		else
		{
			uint modrmSize = X86AddressingHelper.GetInstructionLength( modrm, core, instrBase ) - 1;
			core.Registers["eip"] = immOffset + 4;
		}
	}

	// ── Flag helpers ─────────────────────────────────────────────────────────

	private static void SetFlagsAdd( X86Core core, uint dst, uint src, uint result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = (ulong)dst + src > 0xFFFFFFFF;
		bool ds = (dst & 0x80000000) != 0;
		bool ss = (src & 0x80000000) != 0;
		bool rs = (result & 0x80000000) != 0;
		core.OverflowFlag = (ds == ss) && (rs != ds);
	}

	private static void SetFlagsSub( X86Core core, uint dst, uint src, uint result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = dst < src;
		bool ds = (dst & 0x80000000) != 0;
		bool ss = (src & 0x80000000) != 0;
		bool rs = (result & 0x80000000) != 0;
		core.OverflowFlag = (ds != ss) && (rs != ds);
	}

	private static void SetFlagsLogic( X86Core core, uint result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = false;
		core.OverflowFlag = false;
	}


}
