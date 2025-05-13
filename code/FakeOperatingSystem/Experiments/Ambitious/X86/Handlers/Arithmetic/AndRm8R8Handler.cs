using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class AndRm8R8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x20;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );

		int mod = (modrm >> 6) & 0x3;
		int reg = (modrm >> 3) & 0x7;
		int rm = modrm & 0x7;

		int modrmLen = 1; // ModRM byte itself

		// Get source value (r8)
		byte srcVal = GetRegister8( core, reg );

		// Get destination value (r/m8)
		byte destVal;
		uint destAddr = 0;
		bool isReg = false;

		if ( mod == 3 )
		{
			// Register-direct mode
			destVal = GetRegister8( core, rm );
			isReg = true;
		}
		else
		{
			// Memory mode (no SIB/disp support for simplicity, add as needed)
			destAddr = GetEffectiveAddress( core, eip + 2, mod, rm, out int eaLen );
			destVal = core.ReadByte( destAddr );
			modrmLen += eaLen;
		}

		byte result = (byte)(destVal & srcVal);

		// Write result back
		if ( isReg )
			SetRegister8( core, rm, result );
		else
			core.WriteByte( destAddr, result );

		// Set flags (ZF, SF, PF, CF=0, OF=0)
		core.ZeroFlag = (result == 0);
		core.SignFlag = (result & 0x80) != 0;
		core.ParityFlag = CalculateParity( result );
		core.CarryFlag = false;
		core.OverflowFlag = false;

		// Advance EIP
		core.Registers["eip"] += (uint)(1 + modrmLen);

		if ( X86Core.VerboseLogging )
		{
			core.LogVerbose( $"AND r/m8, r8: 0x{destVal:X2} & 0x{srcVal:X2} = 0x{result:X2}" );
		}
	}

	// Helper: Get 8-bit register value by index (0=AL, 1=CL, 2=DL, 3=BL, 4=AH, 5=CH, 6=DH, 7=BH)
	private byte GetRegister8( X86Core core, int index )
	{
		switch ( index )
		{
			case 0: return (byte)(core.Registers["eax"] & 0xFF); // AL
			case 1: return (byte)(core.Registers["ecx"] & 0xFF); // CL
			case 2: return (byte)(core.Registers["edx"] & 0xFF); // DL
			case 3: return (byte)(core.Registers["ebx"] & 0xFF); // BL
			case 4: return (byte)((core.Registers["eax"] >> 8) & 0xFF); // AH
			case 5: return (byte)((core.Registers["ecx"] >> 8) & 0xFF); // CH
			case 6: return (byte)((core.Registers["edx"] >> 8) & 0xFF); // DH
			case 7: return (byte)((core.Registers["ebx"] >> 8) & 0xFF); // BH
			default: throw new ArgumentOutOfRangeException( nameof( index ) );
		}
	}

	// Helper: Set 8-bit register value by index
	private void SetRegister8( X86Core core, int index, byte value )
	{
		switch ( index )
		{
			case 0: core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | value; break; // AL
			case 1: core.Registers["ecx"] = (core.Registers["ecx"] & 0xFFFFFF00) | value; break; // CL
			case 2: core.Registers["edx"] = (core.Registers["edx"] & 0xFFFFFF00) | value; break; // DL
			case 3: core.Registers["ebx"] = (core.Registers["ebx"] & 0xFFFFFF00) | value; break; // BL
			case 4: core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF00FF) | ((uint)value << 8); break; // AH
			case 5: core.Registers["ecx"] = (core.Registers["ecx"] & 0xFFFF00FF) | ((uint)value << 8); break; // CH
			case 6: core.Registers["edx"] = (core.Registers["edx"] & 0xFFFF00FF) | ((uint)value << 8); break; // DH
			case 7: core.Registers["ebx"] = (core.Registers["ebx"] & 0xFFFF00FF) | ((uint)value << 8); break; // BH
			default: throw new ArgumentOutOfRangeException( nameof( index ) );
		}
	}

	// Helper: Calculate effective address for memory operand (simple version, extend as needed)
	private uint GetEffectiveAddress( X86Core core, uint addr, int mod, int rm, out int len )
	{
		// Only handle [EAX], [ECX], [EDX], [EBX], [ESP], [EBP], [ESI], [EDI] with no SIB/disp for now
		len = 0;
		switch ( mod )
		{
			case 0:
				if ( rm == 5 )
				{
					// disp32
					uint disp32 = core.ReadDword( addr );
					len = 4;
					return disp32;
				}
				else
				{
					len = 0;
					return core.Registers[GetRegisterName( rm )];
				}
			case 1:
				// disp8
				sbyte disp8 = (sbyte)core.ReadByte( addr );
				len = 1;
				return (uint)(core.Registers[GetRegisterName( rm )] + disp8);
			case 2:
				// disp32
				uint disp32b = core.ReadDword( addr );
				len = 4;
				return core.Registers[GetRegisterName( rm )] + disp32b;
			default:
				throw new NotImplementedException( "ModRM mode not supported" );
		}
	}

	private string GetRegisterName( int rm )
	{
		return rm switch
		{
			0 => "eax",
			1 => "ecx",
			2 => "edx",
			3 => "ebx",
			4 => "esp",
			5 => "ebp",
			6 => "esi",
			7 => "edi",
			_ => throw new ArgumentOutOfRangeException( nameof( rm ) ),
		};
	}

	private bool CalculateParity( byte value )
	{
		int count = 0;
		for ( int i = 0; i < 8; i++ )
			if ( ((value >> i) & 1) != 0 ) count++;
		return (count % 2) == 0;
	}
}
