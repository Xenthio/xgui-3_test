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

		// This is like 0x81 but with sign-extended 8-bit immediate
		sbyte imm8 = (sbyte)core.ReadByte( eip + 2 );
		uint signExtImm = (uint)imm8; // Sign-extended to 32 bits

		if ( mod == 3 ) // Register operand
		{
			string destReg = GetRegisterName( rm );
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
			// Advance EIP by the correct instruction length
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip ) + 1;
			core.Registers["eip"] += len;
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

	private string GetRegisterName( int code ) => code switch
	{
		0 => "eax",
		1 => "ecx",
		2 => "edx",
		3 => "ebx",
		4 => "esp",
		5 => "ebp",
		6 => "esi",
		7 => "edi",
		_ => throw new ArgumentException( $"Invalid register code: {code}" )
	};
}
