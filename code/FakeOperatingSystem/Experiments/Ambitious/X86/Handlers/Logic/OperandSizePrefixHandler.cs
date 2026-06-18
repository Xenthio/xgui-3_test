using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class OperandSizePrefixHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x66;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];

		// Skip the prefix byte
		core.Registers["eip"]++;

		// Look at the next byte - often this prefix is used with common instructions
		byte nextByte = core.ReadByte( eip + 1 );

		// Handle common combinations
		switch ( nextByte )
		{
			case 0x03: // ADD r16, r/m16
				Handle66_ADD_R16_Rm16( core );
				break;

			case 0x89: // MOV r/m16, r16 (16-bit version of MOV r/m32, r32)
				Handle66_MOV_Rm16_R16( core );
				break;

			case 0x8B: // MOV r16, r/m16 (16-bit version of MOV r32, r/m32)
				Handle66_MOV_R16_Rm16( core );
				break;

			case 0x0F: // Two-byte opcode with operand size prefix
				Handle66_0F_Prefix( core );
				break;

			case 0x83: // Immediate Group 1 with sign-extended imm8 to 16-bit
			case 0x81: // Immediate Group 1 with 16-bit immediate
				Handle66_Immediate_Group1( core, nextByte );
				break;
			case 0x85: // TEST r/m16, r16 (with 0x66 prefix)
				Handle66_TEST_Rm16_R16( core );
				break;

			case 0xA1: // MOV AX, [moffs32] (16-bit load from absolute address)
				Handle66_MOV_AX_Moffs32( core );
				break;

			case 0xA3: // MOV [moffs32], AX (16-bit store)
				Handle66_MOV_Moffs32_AX( core );
				break;

			case 0x40: case 0x41: case 0x42: case 0x43: // INC r16
			case 0x44: case 0x45: case 0x46: case 0x47:
				Handle66_INC_R16( core, nextByte );
				break;

			case 0x48: case 0x49: case 0x4A: case 0x4B: // DEC r16
			case 0x4C: case 0x4D: case 0x4E: case 0x4F:
				Handle66_DEC_R16( core, nextByte );
				break;

			case 0x3D: // CMP AX, imm16
				Handle66_CMP_AX_Imm16( core );
				break;

			case 0x2D: // SUB AX, imm16
				Handle66_SUB_AX_Imm16( core );
				break;

			case 0x05: // ADD AX, imm16
				Handle66_ADD_AX_Imm16( core );
				break;

			case 0x25: // AND AX, imm16
				Handle66_AND_AX_Imm16( core );
				break;

			case 0x0D: // OR AX, imm16
				Handle66_OR_AX_Imm16( core );
				break;

			case 0x35: // XOR AX, imm16
				Handle66_XOR_AX_Imm16( core );
				break;

			case 0x50: case 0x51: case 0x52: case 0x53: // PUSH r16
			case 0x54: case 0x55: case 0x56: case 0x57:
				Handle66_PUSH_R16( core, nextByte );
				break;

			case 0x58: case 0x59: case 0x5A: case 0x5B: // POP r16
			case 0x5C: case 0x5D: case 0x5E: case 0x5F:
				Handle66_POP_R16( core, nextByte );
				break;

			case 0x39: // CMP r/m16, r16
				Handle66_CMP_Rm16_R16( core );
				break;

			case 0x3B: // CMP r16, r/m16
				Handle66_CMP_R16_Rm16( core );
				break;

			case 0x29: // SUB r/m16, r16
				Handle66_SUB_Rm16_R16( core );
				break;

			case 0x2B: // SUB r16, r/m16
				Handle66_SUB_R16_Rm16( core );
				break;

			case 0x01: // ADD r/m16, r16
				Handle66_ADD_Rm16_R16( core );
				break;

			case 0x09: // OR r/m16, r16
				Handle66_OR_Rm16_R16( core );
				break;

			case 0x0B: // OR r16, r/m16
				Handle66_OR_R16_Rm16( core );
				break;

			case 0x31: // XOR r/m16, r16
				Handle66_XOR_Rm16_R16( core );
				break;

			case 0x33: // XOR r16, r/m16
				Handle66_XOR_R16_Rm16( core );
				break;

			case 0x21: // AND r/m16, r16
				Handle66_AND_Rm16_R16( core );
				break;

			case 0x23: // AND r16, r/m16
				Handle66_AND_R16_Rm16( core );
				break;

			case 0xB8: case 0xB9: case 0xBA: case 0xBB: // MOV r16, imm16
			case 0xBC: case 0xBD: case 0xBE: case 0xBF:
				Handle66_MOV_R16_Imm16( core, nextByte );
				break;

			case 0xC7: // MOV r/m16, imm16
				Handle66_MOV_Rm16_Imm16( core );
				break;

			case 0xC1: // Shift/Rotate r/m16, imm8
				Handle66_ShiftRot_Rm16_Imm8( core );
				break;

			case 0xD3: // Shift/Rotate r/m16, CL
				Handle66_ShiftRot_Rm16_CL( core );
				break;

			case 0xF7: // Unary group: NOT/NEG/MUL/IMUL/DIV/IDIV r/m16
				Handle66_UnaryRm16( core );
				break;

			case 0xFF: // INC/DEC/CALL/JMP/PUSH r/m16
				Handle66_OpcodeFF_Rm16( core );
				break;

			case 0x6B: // IMUL r16, r/m16, imm8
				Handle66_IMUL_R16_Rm16_Imm8( core );
				break;

			case 0x69: // IMUL r16, r/m16, imm16
				Handle66_IMUL_R16_Rm16_Imm16( core );
				break;

			case 0xAA: // STOSB (same as without prefix, but listed for completeness)
				Handle66_STOSB( core );
				break;

			case 0xAB: // STOSW - store AX to [EDI], advance EDI by 2
				Handle66_STOSW( core );
				break;

			case 0xA4: // MOVSB (byte, same regardless of prefix)
				Handle66_MOVSB( core );
				break;

			case 0xA5: // MOVSW - move word [ESI] to [EDI], advance both by 2
				Handle66_MOVSW( core );
				break;

			default:
				// For unhandled combinations, we'll log and skip the instruction
				Log.Warning( $"EIP=0x{eip:X8}: Unhandled 0x66 prefix combination: 0x66 0x{nextByte:X2}" );
				// Skip the next byte too (the instruction after the prefix)
				core.Registers["eip"]++;
				break;
		}
	}

	private void Handle66_ADD_R16_Rm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		string destReg = Get16BitRegisterName( reg );
		ushort dest = (ushort)(core.Registers[destReg] & 0xFFFF);
		ushort src;
		if ( mod == 3 )
		{
			src = (ushort)(core.Registers[Get16BitRegisterName( rm )] & 0xFFFF);
			core.Registers["eip"] += 2;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			src = core.ReadWord( addr );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
		}
		uint result = (uint)(dest + src);
		core.CarryFlag = result > 0xFFFF;
		result &= 0xFFFF;
		core.OverflowFlag = ((dest ^ (ushort)result) & (src ^ (ushort)result) & 0x8000) != 0;
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x8000) != 0;
		core.Registers[destReg] = (core.Registers[destReg] & 0xFFFF0000) | result;
	}

	private void Handle66_MOV_Rm16_R16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		ushort regValue = (ushort)(core.Registers[Get16BitRegisterName( reg )] & 0xFFFF);

		if ( mod == 3 )
		{
			// Register to register
			string destReg = Get16BitRegisterName( rm );
			core.Registers[destReg] = (core.Registers[destReg] & 0xFFFF0000) | regValue;
			core.Registers["eip"] += 2;
			Log.Info( $"16-bit MOV {destReg}, {Get16BitRegisterName( reg )} (reg-reg)" );
		}
		else
		{
			// Register to memory
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			core.WriteWord( addr, regValue );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
			Log.Info( $"16-bit MOV [0x{addr:X8}], {Get16BitRegisterName( reg )} (reg-mem)" );
		}
	}

	private void Handle66_MOV_R16_Rm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		if ( mod == 3 )
		{
			// Register to register
			string destReg = Get16BitRegisterName( reg );
			string srcReg = Get16BitRegisterName( rm );
			ushort value = (ushort)(core.Registers[srcReg] & 0xFFFF);
			core.Registers[destReg] = (core.Registers[destReg] & 0xFFFF0000) | value;
			core.Registers["eip"] += 2;
			Log.Info( $"16-bit MOV {destReg}, {srcReg} (reg-reg)" );
		}
		else
		{
			// Memory to register
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			ushort value = core.ReadWord( addr );
			string destReg = Get16BitRegisterName( reg );
			core.Registers[destReg] = (core.Registers[destReg] & 0xFFFF0000) | value;
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
			Log.Info( $"16-bit MOV {destReg}, [0x{addr:X8}] (mem-reg)" );
		}
	}

	private void Handle66_0F_Prefix( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte thirdByte = core.ReadByte( eip + 2 );

		// This is a 3-byte instruction sequence: 0x66 0x0F <op>
		// Common for SSE/SSE2 instructions with 16-bit operand size override

		switch ( thirdByte )
		{
			case 0x29: // MOVAPS - Move Aligned Packed Single-Precision
			case 0x7F: // MOVDQA - Move Aligned Double Quadword
				core.Registers["eip"] += 3; // Skip the 3-byte opcode
				Log.Info( $"16-bit SSE instruction: 0x66 0x0F 0x{thirdByte:X2} (stub implementation)" );
				break;

			default:
				Log.Warning( $"Unhandled 16-bit SSE instruction: 0x66 0x0F 0x{thirdByte:X2}" );
				core.Registers["eip"] += 3;
				break;
		}
	}

	private void Handle66_Immediate_Group1( X86Core core, byte opcode )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7); // arithmetic op selector
		byte rm  = (byte)(modrm & 0x7);
		uint instrLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );

		ushort value;
		if ( mod == 3 )
			value = (ushort)(core.Registers[Get16BitRegisterName( rm )] & 0xFFFF);
		else
			value = core.ReadWord( X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip ) );

		ushort imm;
		uint skipBytes;
		if ( opcode == 0x83 )
		{
			// sign-extend imm8 to 16 bits
			sbyte imm8 = (sbyte)core.ReadByte( eip + instrLen );
			imm = (ushort)(short)imm8;
			skipBytes = instrLen + 1;
		}
		else // 0x81 - 16-bit immediate
		{
			imm = core.ReadWord( eip + instrLen );
			skipBytes = instrLen + 2;
		}

		ushort result;
		bool writeBack = true;
		switch ( reg )
		{
			case 0: result = (ushort)(value + imm); Set16FlagsAdd( core, value, imm, result ); break;
			case 1: result = (ushort)(value | imm); Set16FlagsLogic( core, result ); break;
			case 2: { ushort c = (ushort)(core.CarryFlag ? 1 : 0); result = (ushort)(value + imm + c); Set16FlagsAdd( core, value, imm, result ); break; }
			case 3: { ushort b = (ushort)(core.CarryFlag ? 1 : 0); result = (ushort)(value - imm - b); Set16FlagsSub( core, value, imm, result ); break; }
			case 4: result = (ushort)(value & imm); Set16FlagsLogic( core, result ); break;
			case 5: result = (ushort)(value - imm); Set16FlagsSub( core, value, imm, result ); break;
			case 6: result = (ushort)(value ^ imm); Set16FlagsLogic( core, result ); break;
			case 7: result = (ushort)(value & imm); Set16FlagsLogic( core, result ); writeBack = false; break; // CMP
			default: result = 0; break;
		}

		if ( writeBack )
		{
			if ( mod == 3 )
				core.Registers[Get16BitRegisterName( rm )] = (core.Registers[Get16BitRegisterName( rm )] & 0xFFFF0000) | result;
			else
				core.WriteWord( X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip ), result );
		}
		core.Registers["eip"] += skipBytes;
	}

	private void Handle66_TEST_Rm16_R16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		ushort value1, value2;
		if ( mod == 3 )
		{
			// Register-direct: TEST reg16, reg16
			string regName1 = Get16BitRegisterName( rm );
			string regName2 = Get16BitRegisterName( reg );
			value1 = (ushort)(core.Registers[regName1] & 0xFFFF);
			value2 = (ushort)(core.Registers[regName2] & 0xFFFF);
			core.Registers["eip"] += 2;
			Log.Info( $"16-bit TEST {regName1}, {regName2} (reg-reg)" );
		}
		else
		{
			// Memory operand: TEST [mem], reg16
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			value1 = core.ReadWord( addr );
			string regName2 = Get16BitRegisterName( reg );
			value2 = (ushort)(core.Registers[regName2] & 0xFFFF);
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
			Log.Info( $"16-bit TEST [0x{addr:X8}], {regName2} (mem-reg)" );
		}

		ushort result = (ushort)(value1 & value2);
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x8000) != 0;
		core.CarryFlag = false;
		core.OverflowFlag = false;
	}

	private void Handle66_MOV_Moffs32_AX( X86Core core )
	{
		uint eip = core.Registers["eip"];
		uint offset = core.ReadDword( eip + 1 );
		ushort axValue = (ushort)(core.Registers["eax"] & 0xFFFF);
		core.WriteWord( offset, axValue );
		core.Registers["eip"] += 5; // opcode + offset (4 bytes)
		Log.Info( $"16-bit MOV [0x{offset:X8}], AX (moffs32-AX)" );
	}

	private string Get16BitRegisterName( int code ) => code switch
	{
		0 => "eax", // AX
		1 => "ecx", // CX
		2 => "edx", // DX
		3 => "ebx", // BX
		4 => "esp", // SP
		5 => "ebp", // BP
		6 => "esi", // SI
		7 => "edi", // DI
		_ => throw new ArgumentException( $"Invalid 16-bit register code: {code}" )
	};

	// --- flag helpers ---
	private void Set16FlagsAdd( X86Core core, ushort a, ushort b, ushort result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x8000) != 0;
		core.CarryFlag = (uint)a + b > 0xFFFF;
		core.OverflowFlag = ((~(a ^ b)) & (a ^ result) & 0x8000) != 0;
	}
	private void Set16FlagsSub( X86Core core, ushort a, ushort b, ushort result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x8000) != 0;
		core.CarryFlag = a < b;
		core.OverflowFlag = ((a ^ b) & (a ^ result) & 0x8000) != 0;
	}
	private void Set16FlagsLogic( X86Core core, ushort result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x8000) != 0;
		core.CarryFlag = false;
		core.OverflowFlag = false;
	}

	// --- AX,moffs32 ---
	private void Handle66_MOV_AX_Moffs32( X86Core core )
	{
		uint eip = core.Registers["eip"];
		uint addr = core.ReadDword( eip + 1 );
		ushort val = core.ReadWord( addr );
		core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | val;
		core.Registers["eip"] += 5;
	}

	// --- INC/DEC r16 ---
	private void Handle66_INC_R16( X86Core core, byte opcode )
	{
		int idx = opcode - 0x40;
		string reg = Get16BitRegisterName( idx );
		ushort val = (ushort)(core.Registers[reg] & 0xFFFF);
		ushort res = (ushort)(val + 1);
		core.OverflowFlag = val == 0x7FFF;
		core.ZeroFlag = res == 0;
		core.SignFlag = (res & 0x8000) != 0;
		core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | res;
		core.Registers["eip"] += 2;
	}
	private void Handle66_DEC_R16( X86Core core, byte opcode )
	{
		int idx = opcode - 0x48;
		string reg = Get16BitRegisterName( idx );
		ushort val = (ushort)(core.Registers[reg] & 0xFFFF);
		ushort res = (ushort)(val - 1);
		core.OverflowFlag = val == 0x8000;
		core.ZeroFlag = res == 0;
		core.SignFlag = (res & 0x8000) != 0;
		core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | res;
		core.Registers["eip"] += 2;
	}

	// --- AX,imm16 arithmetic ---
	private void Handle66_CMP_AX_Imm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		ushort ax  = (ushort)(core.Registers["eax"] & 0xFFFF);
		ushort imm = core.ReadWord( eip + 1 );
		ushort res = (ushort)(ax - imm);
		Set16FlagsSub( core, ax, imm, res );
		core.Registers["eip"] += 3;
	}
	private void Handle66_SUB_AX_Imm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		ushort ax  = (ushort)(core.Registers["eax"] & 0xFFFF);
		ushort imm = core.ReadWord( eip + 1 );
		ushort res = (ushort)(ax - imm);
		Set16FlagsSub( core, ax, imm, res );
		core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | res;
		core.Registers["eip"] += 3;
	}
	private void Handle66_ADD_AX_Imm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		ushort ax  = (ushort)(core.Registers["eax"] & 0xFFFF);
		ushort imm = core.ReadWord( eip + 1 );
		ushort res = (ushort)(ax + imm);
		Set16FlagsAdd( core, ax, imm, res );
		core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | res;
		core.Registers["eip"] += 3;
	}
	private void Handle66_AND_AX_Imm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		ushort ax  = (ushort)(core.Registers["eax"] & 0xFFFF);
		ushort imm = core.ReadWord( eip + 1 );
		ushort res = (ushort)(ax & imm);
		Set16FlagsLogic( core, res );
		core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | res;
		core.Registers["eip"] += 3;
	}
	private void Handle66_OR_AX_Imm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		ushort ax  = (ushort)(core.Registers["eax"] & 0xFFFF);
		ushort imm = core.ReadWord( eip + 1 );
		ushort res = (ushort)(ax | imm);
		Set16FlagsLogic( core, res );
		core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | res;
		core.Registers["eip"] += 3;
	}
	private void Handle66_XOR_AX_Imm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		ushort ax  = (ushort)(core.Registers["eax"] & 0xFFFF);
		ushort imm = core.ReadWord( eip + 1 );
		ushort res = (ushort)(ax ^ imm);
		Set16FlagsLogic( core, res );
		core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | res;
		core.Registers["eip"] += 3;
	}

	// --- PUSH/POP r16 ---
	private void Handle66_PUSH_R16( X86Core core, byte opcode )
	{
		int idx = opcode - 0x50;
		ushort val = (ushort)(core.Registers[Get16BitRegisterName( idx )] & 0xFFFF);
		core.Registers["esp"] -= 2;
		core.WriteWord( core.Registers["esp"], val );
		core.Registers["eip"] += 2;
	}
	private void Handle66_POP_R16( X86Core core, byte opcode )
	{
		int idx = opcode - 0x58;
		string reg = Get16BitRegisterName( idx );
		ushort val = core.ReadWord( core.Registers["esp"] );
		core.Registers["esp"] += 2;
		core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | val;
		core.Registers["eip"] += 2;
	}

	// --- r/m16, r16 and r16, r/m16 helpers (generic 16-bit ALU with modrm) ---
	private void Handle66_ALU_Rm16_R16( X86Core core, Func<ushort,ushort,ushort> op, Action<X86Core,ushort,ushort,ushort> setFlags, bool writeBack = true )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);
		ushort src = (ushort)(core.Registers[Get16BitRegisterName( reg )] & 0xFFFF);
		uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		if ( mod == 3 )
		{
			string destReg = Get16BitRegisterName( rm );
			ushort dst = (ushort)(core.Registers[destReg] & 0xFFFF);
			ushort res = op( dst, src );
			setFlags( core, dst, src, res );
			if ( writeBack ) core.Registers[destReg] = (core.Registers[destReg] & 0xFFFF0000) | res;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			ushort dst = core.ReadWord( addr );
			ushort res = op( dst, src );
			setFlags( core, dst, src, res );
			if ( writeBack ) core.WriteWord( addr, res );
		}
		core.Registers["eip"] += len;
	}
	private void Handle66_ALU_R16_Rm16( X86Core core, Func<ushort,ushort,ushort> op, Action<X86Core,ushort,ushort,ushort> setFlags, bool writeBack = true )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);
		string destReg = Get16BitRegisterName( reg );
		ushort dst = (ushort)(core.Registers[destReg] & 0xFFFF);
		uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		ushort src;
		if ( mod == 3 )
			src = (ushort)(core.Registers[Get16BitRegisterName( rm )] & 0xFFFF);
		else
			src = core.ReadWord( X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip ) );
		ushort res = op( dst, src );
		setFlags( core, dst, src, res );
		if ( writeBack ) core.Registers[destReg] = (core.Registers[destReg] & 0xFFFF0000) | res;
		core.Registers["eip"] += len;
	}

	private void Handle66_CMP_Rm16_R16( X86Core core ) => Handle66_ALU_Rm16_R16( core, (a,b)=>(ushort)(a-b), Set16FlagsSub, false );
	private void Handle66_CMP_R16_Rm16( X86Core core ) => Handle66_ALU_R16_Rm16( core, (a,b)=>(ushort)(a-b), Set16FlagsSub, false );
	private void Handle66_SUB_Rm16_R16( X86Core core ) => Handle66_ALU_Rm16_R16( core, (a,b)=>(ushort)(a-b), Set16FlagsSub );
	private void Handle66_SUB_R16_Rm16( X86Core core ) => Handle66_ALU_R16_Rm16( core, (a,b)=>(ushort)(a-b), Set16FlagsSub );
	private void Handle66_ADD_Rm16_R16( X86Core core ) => Handle66_ALU_Rm16_R16( core, (a,b)=>(ushort)(a+b), Set16FlagsAdd );
	private void Handle66_OR_Rm16_R16 ( X86Core core ) => Handle66_ALU_Rm16_R16( core, (a,b)=>(ushort)(a|b), Set16FlagsLogic3 );
	private void Handle66_OR_R16_Rm16 ( X86Core core ) => Handle66_ALU_R16_Rm16( core, (a,b)=>(ushort)(a|b), Set16FlagsLogic3 );
	private void Handle66_XOR_Rm16_R16( X86Core core ) => Handle66_ALU_Rm16_R16( core, (a,b)=>(ushort)(a^b), Set16FlagsLogic3 );
	private void Handle66_XOR_R16_Rm16( X86Core core ) => Handle66_ALU_R16_Rm16( core, (a,b)=>(ushort)(a^b), Set16FlagsLogic3 );
	private void Handle66_AND_Rm16_R16( X86Core core ) => Handle66_ALU_Rm16_R16( core, (a,b)=>(ushort)(a&b), Set16FlagsLogic3 );
	private void Handle66_AND_R16_Rm16( X86Core core ) => Handle66_ALU_R16_Rm16( core, (a,b)=>(ushort)(a&b), Set16FlagsLogic3 );

	// adapter so void(core,a,b,result) signature works with Set16FlagsLogic
	private void Set16FlagsLogic3( X86Core core, ushort a, ushort b, ushort result ) => Set16FlagsLogic( core, result );

	// --- MOV r16, imm16 ---
	private void Handle66_MOV_R16_Imm16( X86Core core, byte opcode )
	{
		uint eip = core.Registers["eip"];
		int idx = opcode - 0xB8;
		string reg = Get16BitRegisterName( idx );
		ushort imm = core.ReadWord( eip + 1 );
		core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | imm;
		core.Registers["eip"] += 3;
	}

	/// 0x66 0xC7 /0 - MOV r/m16, imm16
	private void Handle66_MOV_Rm16_Imm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		// eip already points PAST the 0x66 prefix (prefix handler did eip++)
		// At eip: 0xC7, eip+1: modrm, eip+2+: sib/disp, then imm16
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte rm  = (byte)(modrm & 0x7);

		// Instruction length (opcode=C7 at eip + modrm) then imm16 (2 bytes)
		uint baseLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		ushort imm = core.ReadWord( eip + baseLen );

		if ( mod == 3 )
		{
			string reg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | imm;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			core.WriteByte( addr,     (byte)(imm & 0xFF) );
			core.WriteByte( addr + 1, (byte)(imm >> 8)   );
		}

		core.Registers["eip"] += baseLen + 2; // + 2 for imm16
	}

	/// 0x66 0xC1 /reg - Shift/Rotate r/m16, imm8
	private void Handle66_ShiftRot_Rm16_Imm8( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte op  = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);
		uint baseLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		byte count = core.ReadByte( eip + baseLen );

		ushort val;
		if ( mod == 3 )
		{
			string reg = X86AddressingHelper.GetRegisterName( rm );
			val = (ushort)(core.Registers[reg] & 0xFFFF);
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			val = (ushort)(core.ReadByte( addr ) | (core.ReadByte( addr+1 ) << 8));
		}

		ushort result;
		int cnt = count & 0x1F;
		switch ( op )
		{
			case 4: result = (ushort)(val << cnt); break; // SHL
			case 5: result = (ushort)(val >> cnt); break; // SHR
			case 7: result = (ushort)((short)val >> cnt); break; // SAR
			case 0: // ROL
				result = cnt == 0 ? val : (ushort)((val << cnt) | (val >> (16 - cnt))); break;
			case 1: // ROR
				result = cnt == 0 ? val : (ushort)((val >> cnt) | (val << (16 - cnt))); break;
			default: result = val; break;
		}
		core.ZeroFlag  = result == 0;
		core.SignFlag  = (result & 0x8000) != 0;
		core.CarryFlag = cnt > 0 && (op == 5 ? (val >> (cnt-1) & 1) != 0 : (val << (cnt-1) & 0x8000) != 0);

		if ( mod == 3 )
		{
			string reg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | result;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			core.WriteByte( addr,     (byte)(result & 0xFF) );
			core.WriteByte( addr + 1, (byte)(result >> 8) );
		}
		core.Registers["eip"] += baseLen + 1; // + 1 for imm8
	}

	/// 0x66 0xD3 /reg - Shift/Rotate r/m16, CL
	private void Handle66_ShiftRot_Rm16_CL( X86Core core )
	{
		// Same as C1 variant but count = CL
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte op  = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);
		uint baseLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		byte count = (byte)(core.Registers["ecx"] & 0xFF);

		ushort val;
		if ( mod == 3 )
		{
			string reg = X86AddressingHelper.GetRegisterName( rm );
			val = (ushort)(core.Registers[reg] & 0xFFFF);
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			val = (ushort)(core.ReadByte( addr ) | (core.ReadByte( addr+1 ) << 8));
		}

		ushort result;
		int cnt = count & 0x1F;
		switch ( op )
		{
			case 4: result = (ushort)(val << cnt); break;
			case 5: result = (ushort)(val >> cnt); break;
			case 7: result = (ushort)((short)val >> cnt); break;
			default: result = val; break;
		}
		core.ZeroFlag  = result == 0;
		core.SignFlag  = (result & 0x8000) != 0;

		if ( mod == 3 )
		{
			string reg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | result;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			core.WriteByte( addr,     (byte)(result & 0xFF) );
			core.WriteByte( addr + 1, (byte)(result >> 8) );
		}
		core.Registers["eip"] += baseLen; // no immediate
	}

	/// 0x66 0xF7 /reg - Unary r/m16 operations
	private void Handle66_UnaryRm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte op  = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);
		uint baseLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );

		ushort val;
		uint addr = 0;
		if ( mod == 3 )
		{
			string reg = X86AddressingHelper.GetRegisterName( rm );
			val = (ushort)(core.Registers[reg] & 0xFFFF);
		}
		else
		{
			addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			val = (ushort)(core.ReadByte( addr ) | (core.ReadByte( addr+1 ) << 8));
		}

		uint extraLen = 0;
		ushort result = val;
		switch ( op )
		{
			case 0: // TEST r/m16, imm16
				{ ushort imm = core.ReadWord( eip + baseLen );
				  result = (ushort)(val & imm);
				  core.ZeroFlag = result == 0; core.SignFlag = (result & 0x8000) != 0;
				  core.CarryFlag = core.OverflowFlag = false;
				  extraLen = 2;
				  result = val; } // TEST doesn't write back
				break;
			case 2: // NOT r/m16
				result = (ushort)~val;
				break;
			case 3: // NEG r/m16
				result = (ushort)(0 - val);
				core.CarryFlag = val != 0;
				core.OverflowFlag = val == 0x8000;
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x8000) != 0;
				break;
			case 4: // MUL r/m16 → DX:AX = AX * r/m16
				{ uint ax = core.Registers["eax"] & 0xFFFF;
				  uint prod = ax * val;
				  core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | (prod & 0xFFFF);
				  core.Registers["edx"] = (core.Registers["edx"] & 0xFFFF0000) | (prod >> 16);
				  core.CarryFlag = core.OverflowFlag = (prod >> 16) != 0; }
				result = (ushort)(core.Registers["eax"] & 0xFFFF);
				break;
			case 5: // IMUL r/m16 → DX:AX = AX * r/m16 (signed)
				{ int ax = (short)(core.Registers["eax"] & 0xFFFF);
				  int src = (short)val;
				  int prod = ax * src;
				  core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | (uint)(ushort)prod;
				  core.Registers["edx"] = (core.Registers["edx"] & 0xFFFF0000) | (uint)(ushort)(prod >> 16);
				  core.CarryFlag = core.OverflowFlag = prod != (short)prod; }
				result = (ushort)(core.Registers["eax"] & 0xFFFF);
				break;
			case 6: // DIV r/m16 → AX = DX:AX / r/m16, DX = remainder
				if ( val != 0 )
				{ uint dxax = ((core.Registers["edx"] & 0xFFFF) << 16) | (core.Registers["eax"] & 0xFFFF);
				  core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | (dxax / val);
				  core.Registers["edx"] = (core.Registers["edx"] & 0xFFFF0000) | (dxax % val); }
				result = (ushort)(core.Registers["eax"] & 0xFFFF);
				break;
			case 7: // IDIV r/m16
				if ( val != 0 )
				{ int dxax = (int)(((core.Registers["edx"] & 0xFFFF) << 16) | (core.Registers["eax"] & 0xFFFF));
				  int sv = (short)val;
				  core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | (uint)(ushort)(dxax / sv);
				  core.Registers["edx"] = (core.Registers["edx"] & 0xFFFF0000) | (uint)(ushort)(dxax % sv); }
				result = (ushort)(core.Registers["eax"] & 0xFFFF);
				break;
		}

		if ( op != 0 && op != 4 && op != 5 && op != 6 && op != 7 ) // Write-back for NOT/NEG
		{
			if ( mod == 3 )
			{ string reg = X86AddressingHelper.GetRegisterName( rm );
			  core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | result; }
			else
			{ core.WriteByte( addr, (byte)(result & 0xFF) );
			  core.WriteByte( addr+1, (byte)(result >> 8) ); }
		}

		core.Registers["eip"] += baseLen + extraLen;
	}

	/// 0x66 0xFF /reg - 16-bit INC/DEC/PUSH indirect ops (common: PUSH r/m16)
	private void Handle66_OpcodeFF_Rm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte op  = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);
		uint baseLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );

		ushort val;
		uint addr = 0;
		if ( mod == 3 )
		{ val = (ushort)(core.Registers[X86AddressingHelper.GetRegisterName( rm )] & 0xFFFF); }
		else
		{ addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
		  val = (ushort)(core.ReadByte( addr ) | (core.ReadByte( addr+1 ) << 8)); }

		switch ( op )
		{
			case 0: // INC r/m16
				{ ushort res = (ushort)(val + 1);
				  core.ZeroFlag = res == 0; core.SignFlag = (res & 0x8000) != 0;
				  if ( mod == 3 )
				  { string rn = X86AddressingHelper.GetRegisterName( rm );
					core.Registers[rn] = (core.Registers[rn] & 0xFFFF0000) | res; }
				  else { core.WriteByte(addr,(byte)(res&0xFF)); core.WriteByte(addr+1,(byte)(res>>8)); } }
				break;
			case 1: // DEC r/m16
				{ ushort res = (ushort)(val - 1);
				  core.ZeroFlag = res == 0; core.SignFlag = (res & 0x8000) != 0;
				  if ( mod == 3 )
				  { string rn = X86AddressingHelper.GetRegisterName( rm );
					core.Registers[rn] = (core.Registers[rn] & 0xFFFF0000) | res; }
				  else { core.WriteByte(addr,(byte)(res&0xFF)); core.WriteByte(addr+1,(byte)(res>>8)); } }
				break;
			case 6: // PUSH r/m16
				{ uint esp = core.Registers["esp"] - 2;
				  core.Registers["esp"] = esp;
				  core.WriteByte( esp,   (byte)(val & 0xFF) );
				  core.WriteByte( esp+1, (byte)(val >> 8) ); }
				break;
			// CALL/JMP would need full logic, skip for now
		}

		core.Registers["eip"] += baseLen;
	}

	/// 0x66 0x6B /r imm8 - IMUL r16, r/m16, sign-extended imm8
	private void Handle66_IMUL_R16_Rm16_Imm8( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);
		uint baseLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		sbyte imm = (sbyte)core.ReadByte( eip + baseLen );

		ushort src;
		if ( mod == 3 ) src = (ushort)(core.Registers[X86AddressingHelper.GetRegisterName( rm )] & 0xFFFF);
		else { uint a = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			   src = (ushort)(core.ReadByte(a) | (core.ReadByte(a+1)<<8)); }

		int result = (short)src * imm;
		string dreg = X86AddressingHelper.GetRegisterName( reg );
		core.Registers[dreg] = (core.Registers[dreg] & 0xFFFF0000) | (uint)(ushort)result;
		core.Registers["eip"] += baseLen + 1;
	}

	/// 0x66 0x69 /r imm16 - IMUL r16, r/m16, imm16
	private void Handle66_IMUL_R16_Rm16_Imm16( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);
		uint baseLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		short imm = (short)core.ReadWord( eip + baseLen );

		ushort src;
		if ( mod == 3 ) src = (ushort)(core.Registers[X86AddressingHelper.GetRegisterName( rm )] & 0xFFFF);
		else { uint a = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			   src = (ushort)(core.ReadByte(a) | (core.ReadByte(a+1)<<8)); }

		int result = (short)src * imm;
		string dreg = X86AddressingHelper.GetRegisterName( reg );
		core.Registers[dreg] = (core.Registers[dreg] & 0xFFFF0000) | (uint)(ushort)result;
		core.Registers["eip"] += baseLen + 2;
	}

	private void Handle66_STOSB( X86Core core )
	{
		// STOSB: same as without 0x66 prefix. EIP already advanced past 0x66; skip 0xAA.
		uint edi = core.Registers["edi"];
		byte val = (byte)(core.Registers["eax"] & 0xFF);
		core.WriteByte( edi, val );
		core.Registers["edi"] = (uint)(edi + (core.DirectionFlag ? -1 : 1));
		core.Registers["eip"] += 1; // skip 0xAA (0x66 already consumed by outer ++)
	}

	private void Handle66_STOSW( X86Core core )
	{
		// STOSW: store AX (low 16 bits of EAX) to [EDI], advance EDI by 2.
		// EIP already advanced past 0x66; we just skip 0xAB here.
		uint edi = core.Registers["edi"];
		ushort val = (ushort)(core.Registers["eax"] & 0xFFFF);
		core.WriteWord( edi, val );
		core.Registers["edi"] = (uint)(edi + (core.DirectionFlag ? -2 : 2));
		core.Registers["eip"] += 1; // skip 0xAB (0x66 already consumed by outer ++)
	}

	private void Handle66_MOVSB( X86Core core )
	{
		// MOVSB with 66 prefix: same as without (byte). Skip 0xA4.
		uint esi = core.Registers["esi"];
		uint edi = core.Registers["edi"];
		core.WriteByte( edi, core.ReadByte( esi ) );
		int delta = core.DirectionFlag ? -1 : 1;
		core.Registers["esi"] = (uint)(esi + delta);
		core.Registers["edi"] = (uint)(edi + delta);
		core.Registers["eip"] += 1;
	}

	private void Handle66_MOVSW( X86Core core )
	{
		// MOVSW: copy word from [ESI] to [EDI], advance both by 2. Skip 0xA5.
		uint esi = core.Registers["esi"];
		uint edi = core.Registers["edi"];
		core.WriteWord( edi, core.ReadWord( esi ) );
		int delta = core.DirectionFlag ? -2 : 2;
		core.Registers["esi"] = (uint)(esi + delta);
		core.Registers["edi"] = (uint)(edi + delta);
		core.Registers["eip"] += 1;
	}
}


