using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class ShiftRotateHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xC0 || opcode == 0xC1 ||
										  opcode == 0xD0 || opcode == 0xD1 ||
										  opcode == 0xD2 || opcode == 0xD3;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7); // Operation type
		byte rm = (byte)(modrm & 0x7);

		// Get the value to shift and count
		uint value = 0;
		byte count = 0;

		if ( mod == 3 ) // Register operand
		{
			string rmReg = GetRegisterName( rm );
			value = core.Registers[rmReg];

			switch ( opcode )
			{
				case 0xC0: // Shift r/m8, imm8
				case 0xC1: // Shift r/m32, imm8
					count = core.ReadByte( eip + 2 );
					core.Registers["eip"] += 3;
					break;

				case 0xD0: // Shift r/m8, 1
				case 0xD1: // Shift r/m32, 1
					count = 1;
					core.Registers["eip"] += 2;
					break;

				case 0xD2: // Shift r/m8, CL
				case 0xD3: // Shift r/m32, CL
					count = (byte)(core.Registers["ecx"] & 0xFF);
					core.Registers["eip"] += 2;
					break;
			}

			// Perform the operation based on 'reg'
			uint result = 0;
			switch ( reg )
			{
				case 0: // ROL - Rotate Left
					if ( count > 0 )
					{
						count %= 32; // Normalize count for 32-bit operand
						result = (value << count) | (value >> (32 - count));
						core.CarryFlag = (result & 1) == 1;
					}
					else result = value;
					break;

				case 1: // ROR - Rotate Right
					if ( count > 0 )
					{
						count %= 32;
						result = (value >> count) | (value << (32 - count));
						core.CarryFlag = ((result >> 31) & 1) == 1;
					}
					else result = value;
					break;

				case 4: // SHL/SAL - Shift Left
					result = value << count;
					core.CarryFlag = count > 0 && ((value >> (32 - count)) & 1) == 1;
					core.ZeroFlag = result == 0;
					core.SignFlag = ((result >> 31) & 1) == 1;
					break;

				case 5: // SHR - Logical Shift Right
					result = value >> count;
					core.CarryFlag = count > 0 && ((value >> (count - 1)) & 1) == 1;
					core.ZeroFlag = result == 0;
					core.SignFlag = ((result >> 31) & 1) == 1;
					break;

				case 7: // SAR - Arithmetic Shift Right
						// Preserve sign bit during shift
					if ( count > 0 )
					{
						// Sign bit
						bool signBit = ((value >> 31) & 1) == 1;
						result = value >> count;
						if ( signBit )
						{
							// Set the upper bits that were shifted out
							result |= ~((1u << (32 - count)) - 1);
						}
						core.CarryFlag = ((value >> (count - 1)) & 1) == 1;
					}
					else result = value;

					core.ZeroFlag = result == 0;
					core.SignFlag = ((result >> 31) & 1) == 1;
					break;

				default:
					Log.Warning( $"Unimplemented shift operation: {reg}" );
					result = value;
					break;
			}

			core.Registers[rmReg] = result;
		}
		else
		{
			// Memory operands not implemented for simplicity
			Log.Warning( "Shift with memory operand not implemented" );
			core.Registers["eip"] += 3;
		}
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

