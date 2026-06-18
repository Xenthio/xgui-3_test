using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// 0x0F prefix (two-byte opcode) handler.
/// Fixed: MOVSX fully implemented; all SETcc (0x90–0x9F) handled generically;
/// MOVZX EIP corrected; CMOVcc range 0x40–0x4F handled generically;
/// IMUL 0x0F AF flags corrected.
/// </summary>
public class ExtendedOpcodeHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x0F;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte secondByte = core.ReadByte( eip + 1 );

		// ── Jcc rel32 (0x0F 0x80–0x8F) ───────────────────────────────────────
		if ( secondByte >= 0x80 && secondByte <= 0x8F )
		{
			HandleConditionalJump32( core, secondByte );
			return;
		}

		// ── CMOVcc (0x0F 0x40–0x4F) ──────────────────────────────────────────
		if ( secondByte >= 0x40 && secondByte <= 0x4F )
		{
			HandleCMov( core, secondByte );
			return;
		}

		// ── SETcc (0x0F 0x90–0x9F) ───────────────────────────────────────────
		if ( secondByte >= 0x90 && secondByte <= 0x9F )
		{
			HandleSetcc( core, secondByte );
			return;
		}

		switch ( secondByte )
		{
			case 0x31: // RDTSC
				core.Registers["edx"] = 0;
				core.Registers["eax"] = (uint)((ulong)System.Diagnostics.Stopwatch.GetTimestamp() * 1000UL / (ulong)System.Diagnostics.Stopwatch.Frequency & 0x7FFFFFFF);
				core.Registers["eip"] += 2;
				break;

			case 0x57: // XORPS xmm1, xmm2/m128 — SSE stub, skip
				core.Registers["eip"] += 3; // 0F 57 modrm
				break;

			case 0xA2: // CPUID
				switch ( core.Registers["eax"] )
				{
					case 0:
						core.Registers["eax"] = 1;
						core.Registers["ebx"] = 0x756E6547; // "Genu"
						core.Registers["edx"] = 0x49656E69; // "ineI"
						core.Registers["ecx"] = 0x6C65746E; // "ntel"
						break;
					case 1:
						core.Registers["eax"] = 0x00000633;
						core.Registers["ebx"] = 0;
						core.Registers["ecx"] = 0;
						core.Registers["edx"] = 0x00000001;
						break;
					default:
						core.Registers["eax"] = core.Registers["ebx"] =
						core.Registers["ecx"] = core.Registers["edx"] = 0;
						break;
				}
				core.Registers["eip"] += 2;
				break;

			case 0xB6: // MOVZX r32, r/m8
			case 0xB7: // MOVZX r32, r/m16
				HandleMovzx( core, secondByte );
				break;

			case 0xBE: // MOVSX r32, r/m8
			case 0xBF: // MOVSX r32, r/m16
				HandleMovsx( core, secondByte );
				break;

			case 0xAF: // IMUL r32, r/m32
				HandleImul2( core );
				break;

			case 0xAC: // SHRD r/m32, r32, imm8
				HandleShrd( core );
				break;

			case 0xAD: // SHRD r/m32, r32, CL
				HandleShrdCl( core );
				break;

			case 0xA4: // SHLD r/m32, r32, imm8
				HandleShld( core );
				break;

			case 0xA5: // SHLD r/m32, r32, CL
				HandleShldCl( core );
				break;

			default:
				Log.Warning( $"Unimplemented extended opcode: 0x0F 0x{secondByte:X2} at EIP=0x{eip:X8}" );
				core.Registers["eip"] += 2;
				break;
		}
	}

	// ── Jcc rel32 ─────────────────────────────────────────────────────────────

	private static void HandleConditionalJump32( X86Core core, byte opcode )
	{
		uint eip = core.Registers["eip"];
		int offset = (int)core.ReadDword( eip + 2 );
		bool cond = EvaluateCondition( opcode & 0xF, core );
		core.Registers["eip"] = cond
			? (uint)((int)(eip + 6) + offset)
			: eip + 6;
	}

	// ── CMOVcc ────────────────────────────────────────────────────────────────

	private static void HandleCMov( X86Core core, byte secondByte )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		bool cond = EvaluateCondition( secondByte & 0xF, core );
		string destReg = X86AddressingHelper.GetRegisterName( reg );

		if ( cond )
		{
			if ( mod == 3 )
				core.Registers[destReg] = core.Registers[X86AddressingHelper.GetRegisterName( rm )];
			else
			{
				uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
				core.Registers[destReg] = core.ReadDword( addr );
			}
		}

		if ( mod == 3 )
			core.Registers["eip"] += 3;
		else
		{
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
			core.Registers["eip"] += 1 + len; // 0F + secondByte + modrm/SIB/disp
		}
	}

	// ── SETcc ─────────────────────────────────────────────────────────────────

	private static void HandleSetcc( X86Core core, byte secondByte )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte rm = (byte)(modrm & 0x7);
		byte value = EvaluateCondition( secondByte & 0xF, core ) ? (byte)1 : (byte)0;

		if ( mod == 3 )
		{
			string rname = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[rname] = (core.Registers[rname] & 0xFFFFFF00) | value;
			core.Registers["eip"] += 3;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
			core.WriteByte( addr, value );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
			core.Registers["eip"] += 1 + len;
		}
	}

	// ── MOVZX ─────────────────────────────────────────────────────────────────

	private static void HandleMovzx( X86Core core, byte opcode )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );

		if ( mod == 3 )
		{
			uint src = core.Registers[X86AddressingHelper.GetRegisterName( rm )];
			core.Registers[destReg] = (opcode == 0xB6) ? (src & 0xFF) : (src & 0xFFFF);
			core.Registers["eip"] += 3;
		}
		else
		{
			// For 0F-prefixed 2-byte opcodes: eip→0F, eip+1→opcode, eip+2→ModRM
			// CalculateEffectiveAddress expects instructionAddress where opcode is,
			// so we pass eip+1 (the actual opcode byte, modrm at +1 from there)
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
			core.Registers[destReg] = (opcode == 0xB6)
				? (uint)core.ReadByte( addr )
				: (uint)core.ReadWord( addr );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
			core.Registers["eip"] += 1 + len; // 0F prefix + (opcode + modrm/SIB/disp)
		}
	}

	// ── MOVSX ─────────────────────────────────────────────────────────────────

	private static void HandleMovsx( X86Core core, byte opcode )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );

		if ( mod == 3 )
		{
			uint src = core.Registers[X86AddressingHelper.GetRegisterName( rm )];
			if ( opcode == 0xBE ) // sign-extend byte→dword
				core.Registers[destReg] = (uint)(int)(sbyte)(byte)(src & 0xFF);
			else                  // sign-extend word→dword
				core.Registers[destReg] = (uint)(int)(short)(ushort)(src & 0xFFFF);
			core.Registers["eip"] += 3;
		}
		else
		{
			// 0F-prefixed: pass eip+1 so CalculateEffectiveAddress finds modrm at +1
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
			if ( opcode == 0xBE )
				core.Registers[destReg] = (uint)(int)(sbyte)core.ReadByte( addr );
			else
				core.Registers[destReg] = (uint)(int)(short)core.ReadWord( addr );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
			core.Registers["eip"] += 1 + len; // 0F prefix + (opcode + modrm/SIB/disp)
		}
	}

	// ── IMUL r32, r/m32 (0x0F AF) ─────────────────────────────────────────────

	private static void HandleImul2( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );
		int dst = (int)core.Registers[destReg];
		int src;

		if ( mod == 3 )
		{
			src = (int)core.Registers[X86AddressingHelper.GetRegisterName( rm )];
			core.Registers["eip"] += 3;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
			src = (int)core.ReadDword( addr );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
			core.Registers["eip"] += 1 + len;
		}

		long full = (long)dst * src;
		uint result = (uint)(full & 0xFFFFFFFF);
		core.Registers[destReg] = result;

		bool overflow = full != (int)result;
		core.OverflowFlag = overflow;
		core.CarryFlag = overflow;
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
	}

	// ── SHRD ──────────────────────────────────────────────────────────────────

	private static void HandleShrd( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		uint src = core.Registers[X86AddressingHelper.GetRegisterName( reg )];

		if ( mod == 3 )
		{
			byte count = (byte)(core.ReadByte( eip + 3 ) & 0x1F);
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			uint dest = core.Registers[destReg];
			uint result = ShrdCalc( core, dest, src, count );
			core.Registers[destReg] = result;
			core.Registers["eip"] += 4;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
			uint modLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 ) - 1;
			byte count = (byte)(core.ReadByte( (uint)(eip + 1 + modLen) ) & 0x1F);
			uint dest = core.ReadDword( addr );
			uint result = ShrdCalc( core, dest, src, count );
			core.WriteDword( addr, result );
			core.Registers["eip"] = (uint)(eip + 1 + modLen + 1 + 1); // 0F AC modrm/sib/disp imm
		}
	}

	private static void HandleShrdCl( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte count = (byte)(core.Registers["ecx"] & 0x1F);
		uint src = core.Registers[X86AddressingHelper.GetRegisterName( reg )];

		if ( mod == 3 )
		{
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[destReg] = ShrdCalc( core, core.Registers[destReg], src, count );
			core.Registers["eip"] += 3;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
			core.WriteDword( addr, ShrdCalc( core, core.ReadDword( addr ), src, count ) );
			core.Registers["eip"] += 1 + len;
		}
	}

	private static uint ShrdCalc( X86Core core, uint dest, uint src, byte count )
	{
		if ( count == 0 ) return dest;
		uint result = (dest >> count) | (src << (32 - count));
		core.CarryFlag = ((dest >> (count - 1)) & 1) != 0;
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		if ( count == 1 )
			core.OverflowFlag = ((result ^ dest) & 0x80000000) != 0;
		return result;
	}

	// ── SHLD ──────────────────────────────────────────────────────────────────

	private static void HandleShld( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		uint src = core.Registers[X86AddressingHelper.GetRegisterName( reg )];

		if ( mod == 3 )
		{
			byte count = (byte)(core.ReadByte( eip + 3 ) & 0x1F);
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[destReg] = ShldCalc( core, core.Registers[destReg], src, count );
			core.Registers["eip"] += 4;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
			uint modLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 ) - 1;
			byte count = (byte)(core.ReadByte( (uint)(eip + 1 + modLen) ) & 0x1F);
			core.WriteDword( addr, ShldCalc( core, core.ReadDword( addr ), src, count ) );
			core.Registers["eip"] = (uint)(eip + 1 + modLen + 1 + 1);
		}
	}

	private static void HandleShldCl( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		byte count = (byte)(core.Registers["ecx"] & 0x1F);
		uint src = core.Registers[X86AddressingHelper.GetRegisterName( reg )];

		if ( mod == 3 )
		{
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[destReg] = ShldCalc( core, core.Registers[destReg], src, count );
			core.Registers["eip"] += 3;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
			core.WriteDword( addr, ShldCalc( core, core.ReadDword( addr ), src, count ) );
			core.Registers["eip"] += 1 + len;
		}
	}

	private static uint ShldCalc( X86Core core, uint dest, uint src, byte count )
	{
		if ( count == 0 ) return dest;
		uint result = (dest << count) | (src >> (32 - count));
		core.CarryFlag = ((dest >> (32 - count)) & 1) != 0;
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		if ( count == 1 )
			core.OverflowFlag = ((result ^ dest) & 0x80000000) != 0;
		return result;
	}

	// ── Shared condition evaluator ────────────────────────────────────────────

	private static bool EvaluateCondition( int cc, X86Core core ) => cc switch
	{
		0x0 => core.OverflowFlag,                                             // O
		0x1 => !core.OverflowFlag,                                            // NO
		0x2 => core.CarryFlag,                                                // B/NAE/C
		0x3 => !core.CarryFlag,                                               // NB/AE/NC
		0x4 => core.ZeroFlag,                                                 // Z/E
		0x5 => !core.ZeroFlag,                                                // NZ/NE
		0x6 => core.ZeroFlag || core.CarryFlag,                               // BE/NA
		0x7 => !core.ZeroFlag && !core.CarryFlag,                             // NBE/A
		0x8 => core.SignFlag,                                                  // S
		0x9 => !core.SignFlag,                                                 // NS
		0xA => core.ParityFlag,                                                // P/PE
		0xB => !core.ParityFlag,                                               // NP/PO
		0xC => core.SignFlag != core.OverflowFlag,                             // L/NGE
		0xD => core.SignFlag == core.OverflowFlag,                             // NL/GE
		0xE => core.ZeroFlag || (core.SignFlag != core.OverflowFlag),          // LE/NG
		0xF => !core.ZeroFlag && (core.SignFlag == core.OverflowFlag),         // NLE/G
		_ => false
	};


}
