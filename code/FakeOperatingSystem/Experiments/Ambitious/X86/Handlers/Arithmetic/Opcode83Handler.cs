using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class Opcode83Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x83;

	public void Execute( X86Core core )
	{
		core.LogVerbose( $"Opcode83Handler: opcode=0x83" );
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7); // Operation type
		byte rm = (byte)(modrm & 0x7);

		// This is like 0x81 but with sign-extended 8-bit immediate.
		// For mod==3 (register), imm8 is at eip+2 (opcode+modrm = 2 bytes).
		// For memory modes, imm8 comes AFTER modrm+SIB+disp bytes — read it after GetInstructionLength.
		// We compute the correct offset below; use a placeholder here.

		if ( mod == 3 ) // Register operand
		{
			// Register mode: layout is opcode(1) + modrm(1) + imm8(1) — imm at eip+2
			sbyte imm8 = (sbyte)core.ReadByte( eip + 2 );
			uint signExtImm = (uint)imm8; // Sign-extended to 32 bits
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			uint value = core.Registers[destReg];
			uint result = 0;

			switch ( reg )
			{
				case 0: // ADD
					result = value + signExtImm;
					SetFlagsAdd( core, value, signExtImm, result );
					core.Registers[destReg] = result;
					core.LogVerbose( $"Add {destReg}, {imm8:X8} = {result:X8}" );
					break;
				case 1: // OR
					result = value | signExtImm;
					SetFlagsLogic( core, result );
					core.Registers[destReg] = result;
					core.LogVerbose( $"Or {destReg}, {imm8:X8} = {result:X8}" );
					break;
				case 2: // ADC
					{
						uint carry = core.CarryFlag ? 1u : 0u;
						ulong full = (ulong)value + signExtImm + carry;
						result = (uint)full;
						SetFlagsAdd( core, value, signExtImm, result );
						core.CarryFlag = full > 0xFFFFFFFF;
						core.Registers[destReg] = result;
					}
					break;
				case 3: // SBB
					{
						uint borrow = core.CarryFlag ? 1u : 0u;
						ulong full = (ulong)value - signExtImm - borrow;
						result = (uint)full;
						SetFlagsSub( core, value, signExtImm, result );
						core.CarryFlag = full > 0xFFFFFFFF;
						core.Registers[destReg] = result;
					}
					break;
				case 4: // AND
					result = value & signExtImm;
					SetFlagsLogic( core, result );
					core.Registers[destReg] = result;
					core.LogVerbose( $"And {destReg}, {imm8:X8} = {result:X8}" );
					break;
				case 5: // SUB
					result = value - signExtImm;
					SetFlagsSub( core, value, signExtImm, result );
					core.Registers[destReg] = result;
					core.LogVerbose( $"Sub {destReg}, {imm8:X8} = {result:X8}" );
					break;
				case 6: // XOR
					result = value ^ signExtImm;
					SetFlagsLogic( core, result );
					core.Registers[destReg] = result;
					core.LogVerbose( $"Xor {destReg}, {imm8:X8} = {result:X8}" );
					break;
				case 7: // CMP
					result = value - signExtImm;
					SetFlagsSub( core, value, signExtImm, result );
					core.LogVerbose( $"Cmp {destReg}, {imm8:X8} = {result:X8}" );
					break;
				default:
					throw new NotImplementedException( $"Opcode 0x83 with reg={reg} not implemented" );
			}
			core.Registers["eip"] += 3;
		}
		else // Memory operand
		{
			// Memory mode: imm8 comes AFTER opcode(1) + modrm(1) + SIB?(1) + disp?(1 or 4)
			// GetInstructionLength(modrm, core, eip) returns the byte count for opcode+modrm+SIB+disp.
			// The imm8 byte sits at eip + that length.
			uint memLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			sbyte imm8 = (sbyte)core.ReadByte( eip + memLen );
			uint signExtImm = (uint)imm8; // Sign-extended to 32 bits

			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint value = core.ReadDword( addr );
			uint result = 0;

			switch ( reg )
			{
				case 0: // ADD
					result = value + signExtImm;
					SetFlagsAdd( core, value, signExtImm, result );
					core.WriteDword( addr, result );
					core.LogVerbose( $"Add [0x{addr:X8}], {imm8:X8} = {result:X8}" );
					break;
				case 1: // OR
					result = value | signExtImm;
					SetFlagsLogic( core, result );
					core.WriteDword( addr, result );
					core.LogVerbose( $"Or [0x{addr:X8}], {imm8:X8} = {result:X8}" );
					break;
				case 2: // ADC
					{
						uint carry = core.CarryFlag ? 1u : 0u;
						ulong full = (ulong)value + signExtImm + carry;
						result = (uint)full;
						SetFlagsAdd( core, value, signExtImm, result );
						core.CarryFlag = full > 0xFFFFFFFF;
						core.WriteDword( addr, result );
					}
					break;
				case 3: // SBB
					{
						uint borrow = core.CarryFlag ? 1u : 0u;
						ulong full = (ulong)value - signExtImm - borrow;
						result = (uint)full;
						SetFlagsSub( core, value, signExtImm, result );
						core.CarryFlag = full > 0xFFFFFFFF;
						core.WriteDword( addr, result );
					}
					break;
					case 4: // AND
					result = value & signExtImm;
					SetFlagsLogic( core, result );
					core.WriteDword( addr, result );
					core.LogVerbose( $"And [0x{addr:X8}], {imm8:X8} = {result:X8}" );
					break;
				case 5: // SUB
					result = value - signExtImm;
					SetFlagsSub( core, value, signExtImm, result );
					core.WriteDword( addr, result );
					core.LogVerbose( $"Sub [0x{addr:X8}], {imm8:X8} = {result:X8}" );
					break;
				case 6: // XOR
					result = value ^ signExtImm;
					SetFlagsLogic( core, result );
					core.WriteDword( addr, result );
					core.LogVerbose( $"Xor [0x{addr:X8}], {imm8:X8} = {result:X8}" );
					break;
				case 7: // CMP
					result = value - signExtImm;
					SetFlagsSub( core, value, signExtImm, result );
					core.LogVerbose( $"Cmp [0x{addr:X8}], {imm8:X8} = {result:X8}" );
					break;
				default:
					throw new NotImplementedException( $"Opcode 0x83 with reg={reg} not implemented" );
			}
			// Advance EIP: opcode(1) already in eip, plus modrm+SIB+disp (=memLen-1) + imm8(1) = memLen
			core.Registers["eip"] += memLen + 1; // +1 for the imm8 byte
		}
	}

	private void SetFlagsAdd( X86Core core, uint dest, uint src, uint result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = result < dest;
		bool destSign = (dest & 0x80000000) != 0;
		bool srcSign = (src & 0x80000000) != 0;
		bool resultSign = (result & 0x80000000) != 0;
		core.OverflowFlag = (destSign == srcSign) && (resultSign != destSign);
	}

	private void SetFlagsSub( X86Core core, uint dest, uint src, uint result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = dest < src;
		bool destSign = (dest & 0x80000000) != 0;
		bool srcSign = (src & 0x80000000) != 0;
		bool resultSign = (result & 0x80000000) != 0;
		core.OverflowFlag = (destSign != srcSign) && (resultSign != destSign);
	}

	private void SetFlagsLogic( X86Core core, uint result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = false;
		core.OverflowFlag = false;
	}


}
