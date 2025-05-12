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

			case 0xA3: // MOV moffs32, AX (with 0x66 prefix)
				Handle66_MOV_Moffs32_AX( core );
				break;

			default:
				// For unhandled combinations, we'll log and skip the instruction
				Log.Warning( $"EIP=0x{eip:X8}: Unhandled 0x66 prefix combination: 0x66 0x{nextByte:X2}" );
				// Skip the next byte too (the instruction after the prefix)
				core.Registers["eip"]++;
				break;
		}
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
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);
		if ( mod == 3 )
		{
			// Register to register
			string destReg = Get16BitRegisterName( reg );
			string srcReg = Get16BitRegisterName( rm );
			core.Registers[destReg] = (core.Registers[destReg] & 0xFFFF0000) | (ushort)(core.Registers[srcReg] & 0xFFFF);
			core.Registers["eip"] += 2;
			Log.Info( $"16-bit Immediate Group 1: {destReg}, {srcReg} (reg-reg)" );
		}
		else
		{
			string destReg = Get16BitRegisterName( reg );
			// Memory to register
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			core.WriteWord( addr, (ushort)(core.Registers[destReg] & 0xFFFF) );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
			Log.Info( $"16-bit Immediate Group 1: [0x{addr:X8}], {Get16BitRegisterName( reg )} (mem-reg)" );
		}
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
}

