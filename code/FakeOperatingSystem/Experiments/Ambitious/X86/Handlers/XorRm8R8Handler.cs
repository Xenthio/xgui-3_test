using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class XorRm8R8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x30;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// Get the 8-bit register value (source)
		byte sourceValue = Get8BitRegisterValue( core, reg );

		if ( mod == 3 ) // Register destination
		{
			// Get destination register (8-bit)
			byte destValue = Get8BitRegisterValue( core, rm );

			// Perform the XOR
			byte result = (byte)(destValue ^ sourceValue);

			// Store the result back in the destination register
			Set8BitRegisterValue( core, rm, result );

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = false; // Always cleared
			core.OverflowFlag = false; // Always cleared

			core.Registers["eip"] += 2;
		}
		else // Memory destination
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			byte destValue = core.ReadByte( effectiveAddress );

			// Perform the XOR
			byte result = (byte)(destValue ^ sourceValue);

			// Store the result back to memory
			core.WriteByte( effectiveAddress, result );

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = false; // Always cleared
			core.OverflowFlag = false; // Always cleared

			// Advance EIP
			uint length = X86AddressingHelper.GetInstructionLength( modrm );
			core.Registers["eip"] += length;
		}

		Log.Info( $"XOR r/m8, r8: result=0x{sourceValue:X2}, ZF={core.ZeroFlag}, SF={core.SignFlag}" );
	}

	private byte Get8BitRegisterValue( X86Core core, byte regCode )
	{
		// Map 8-bit register codes to register names and positions
		// 0=AL, 1=CL, 2=DL, 3=BL, 4=AH, 5=CH, 6=DH, 7=BH
		switch ( regCode )
		{
			case 0: return (byte)(core.Registers["eax"] & 0xFF);        // AL
			case 1: return (byte)(core.Registers["ecx"] & 0xFF);        // CL
			case 2: return (byte)(core.Registers["edx"] & 0xFF);        // DL
			case 3: return (byte)(core.Registers["ebx"] & 0xFF);        // BL
			case 4: return (byte)((core.Registers["eax"] >> 8) & 0xFF); // AH
			case 5: return (byte)((core.Registers["ecx"] >> 8) & 0xFF); // CH
			case 6: return (byte)((core.Registers["edx"] >> 8) & 0xFF); // DH
			case 7: return (byte)((core.Registers["ebx"] >> 8) & 0xFF); // BH
			default: throw new ArgumentException( $"Invalid 8-bit register code: {regCode}" );
		}
	}

	private void Set8BitRegisterValue( X86Core core, byte regCode, byte value )
	{
		string regName;
		uint mask;
		int shiftAmount; // Changed to int instead of uint

		// Map register code to register name, mask, and shift amount
		switch ( regCode )
		{
			case 0: // AL
				regName = "eax";
				mask = 0xFFFFFF00;
				shiftAmount = 0;
				break;
			case 1: // CL
				regName = "ecx";
				mask = 0xFFFFFF00;
				shiftAmount = 0;
				break;
			case 2: // DL
				regName = "edx";
				mask = 0xFFFFFF00;
				shiftAmount = 0;
				break;
			case 3: // BL
				regName = "ebx";
				mask = 0xFFFFFF00;
				shiftAmount = 0;
				break;
			case 4: // AH
				regName = "eax";
				mask = 0xFFFF00FF;
				shiftAmount = 8;
				break;
			case 5: // CH
				regName = "ecx";
				mask = 0xFFFF00FF;
				shiftAmount = 8;
				break;
			case 6: // DH
				regName = "edx";
				mask = 0xFFFF00FF;
				shiftAmount = 8;
				break;
			case 7: // BH
				regName = "ebx";
				mask = 0xFFFF00FF;
				shiftAmount = 8;
				break;
			default:
				throw new ArgumentException( $"Invalid 8-bit register code: {regCode}" );
		}

		// Update the register while preserving other bits
		uint currentValue = core.Registers[regName];
		currentValue = (currentValue & mask) | ((uint)value << shiftAmount);
		core.Registers[regName] = currentValue;
	}
}


