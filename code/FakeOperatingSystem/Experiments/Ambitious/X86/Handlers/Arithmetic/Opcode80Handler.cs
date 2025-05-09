using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class Opcode80Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x80;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7); // operation
		byte rm = (byte)(modrm & 0x7);

		byte imm8 = core.ReadByte( eip + 2 );

		if ( mod == 3 )
		{
			string destReg = Get8BitRegisterName( rm );
			byte value = (byte)(core.Registers[destReg] & 0xFF);
			byte result = 0;

			switch ( reg )
			{
				case 0: // ADD
					result = (byte)(value + imm8);
					break;
				case 1: // OR
					result = (byte)(value | imm8);
					break;
				case 4: // AND
					result = (byte)(value & imm8);
					break;
				case 5: // SUB
					result = (byte)(value - imm8);
					break;
				case 7: // CMP
					result = (byte)(value - imm8);
					// Set flags only, don't store result
					core.ZeroFlag = result == 0;
					core.SignFlag = (result & 0x80) != 0;
					core.CarryFlag = value < imm8;
					core.OverflowFlag = ((value ^ imm8) & (value ^ result) & 0x80) != 0;
					core.Registers["eip"] += 3;
					return;
				default:
					throw new NotImplementedException( $"Opcode 0x80 with reg={reg} not implemented" );
			}

			// Store result in low 8 bits of the register
			core.Registers[destReg] = (core.Registers[destReg] & 0xFFFFFF00) | result;

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = false; // For ADD/SUB, you may want to set this properly
			core.OverflowFlag = false;

			core.Registers["eip"] += 3;
		}
		else
		{
			// Memory operand
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			byte value = core.ReadByte( effectiveAddress );
			byte result = 0;

			switch ( reg )
			{
				case 0: // ADD
					result = (byte)(value + imm8);
					core.WriteByte( effectiveAddress, result );
					break;
				case 1: // OR
					result = (byte)(value | imm8);
					core.WriteByte( effectiveAddress, result );
					break;
				case 4: // AND
					result = (byte)(value & imm8);
					core.WriteByte( effectiveAddress, result );
					break;
				case 5: // SUB
					result = (byte)(value - imm8);
					core.WriteByte( effectiveAddress, result );
					break;
				case 7: // CMP
					result = (byte)(value - imm8);
					// Do not write result for CMP
					break;
				default:
					throw new NotImplementedException( $"Opcode 0x80 with reg={reg} not implemented" );
			}

			// Set flags (minimal, you may want to improve for ADD/SUB)
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = (reg == 5 || reg == 7) ? value < imm8 : false; // SUB/CMP
			core.OverflowFlag = false;

			// Advance EIP by instruction length
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length + 1;
		}
	}

	private string Get8BitRegisterName( int code ) => code switch
	{
		0 => "eax", // AL
		1 => "ecx", // CL
		2 => "edx", // DL
		3 => "ebx", // BL
		4 => "eax", // AH (needs special handling)
		5 => "ecx", // CH
		6 => "edx", // DH
		7 => "ebx", // BH
		_ => throw new ArgumentException( $"Invalid 8-bit register code: {code}" )
	};
}
