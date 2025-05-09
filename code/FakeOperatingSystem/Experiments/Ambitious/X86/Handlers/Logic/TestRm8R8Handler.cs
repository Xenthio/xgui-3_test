using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class TestRm8R8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x84;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// Get the 8-bit register value
		byte sourceValue = Get8BitRegisterValue( core, reg );

		if ( mod == 3 ) // Register destination
		{
			// Get destination register (8-bit)
			byte destValue = Get8BitRegisterValue( core, rm );

			// Perform the TEST (bitwise AND without storing result)
			byte result = (byte)(destValue & sourceValue);

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

			// Perform the TEST (bitwise AND without storing result)
			byte result = (byte)(destValue & sourceValue);

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = false; // Always cleared
			core.OverflowFlag = false; // Always cleared

			// Advance EIP
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length;
		}

		Log.Info( $"TEST r/m8, r8: ZF={core.ZeroFlag}, SF={core.SignFlag}" );
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
}

