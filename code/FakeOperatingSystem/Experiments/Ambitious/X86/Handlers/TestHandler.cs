using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// I dont think this is correct at all...
/// </summary>
public class TestHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x84 || opcode == 0x85 || opcode == 0xA8 || opcode == 0xA9;
	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		switch ( opcode )
		{
			case 0x84: // TEST r/m32, r32
				{
					byte modrm = core.ReadByte( eip + 1 );
					byte mod = (byte)(modrm >> 6);
					byte reg = (byte)((modrm >> 3) & 0x7);
					byte rm = (byte)(modrm & 0x7);
					uint value1 = GetOperandValue( core, mod, reg, rm );
					uint value2 = GetOperandValue( core, mod, rm, reg );
					core.ZeroFlag = (value1 & value2) == 0;
					core.SignFlag = ((value1 & value2) & 0x80000000) != 0;
					core.ParityFlag = CalculateParity( value1 & value2 );
					core.Registers["eip"] += 2;
				}
				break;
			case 0x85: // TEST r32, r/m32
				{
					byte modrm = core.ReadByte( eip + 1 );
					byte mod = (byte)(modrm >> 6);
					byte reg = (byte)((modrm >> 3) & 0x7);
					byte rm = (byte)(modrm & 0x7);
					uint value1 = GetOperandValue( core, mod, reg, rm );
					uint value2 = GetOperandValue( core, mod, rm, reg );
					core.ZeroFlag = (value1 & value2) == 0;
					core.SignFlag = ((value1 & value2) & 0x80000000) != 0;
					core.ParityFlag = CalculateParity( value1 & value2 );
					core.Registers["eip"] += 2;
				}
				break;
			case 0xA8: // TEST AL, imm8
				{
					byte imm8 = core.ReadByte( eip + 1 );
					uint value = core.Registers["eax"] & imm8;
					core.ZeroFlag = value == 0;
					core.SignFlag = (value & 0x80) != 0;
					core.ParityFlag = CalculateParity( value );
					core.Registers["eip"] += 2;
				}
				break;
			case 0xA9: // TEST EAX, imm32
				{
					uint imm32 = core.ReadDword( eip + 1 );
					uint value = core.Registers["eax"] & imm32;
					core.ZeroFlag = value == 0;
					core.SignFlag = (value & 0x80000000) != 0;
					core.ParityFlag = CalculateParity( value );
					core.Registers["eip"] += 5;
				}
				break;
		}
	}
	private uint GetOperandValue( X86Core core, byte mod, byte reg, byte rm )
	{
		if ( mod == 3 ) // Register to register
		{
			string regName = GetRegisterName( rm );
			return core.Registers[regName];
		}
		else
		{
			uint effectiveAddress = CalculateEffectiveAddress( core, mod, rm );
			return core.ReadDword( effectiveAddress );
		}
	}
	private uint CalculateEffectiveAddress( X86Core core, byte mod, byte rm )
	{
		uint effectiveAddress = 0;
		if ( mod == 0 && rm == 5 ) // [disp32]
			effectiveAddress = core.ReadDword( core.Registers["eip"] + 2 );
		else
			effectiveAddress = core.Registers[GetRegisterName( rm )];
		return effectiveAddress;
	}
	private bool CalculateParity( uint value )
	{
		int count = 0;
		for ( int i = 0; i < 32; i++ )
		{
			if ( (value & (1u << i)) != 0 )
				count++;
		}
		return count % 2 == 0;
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
