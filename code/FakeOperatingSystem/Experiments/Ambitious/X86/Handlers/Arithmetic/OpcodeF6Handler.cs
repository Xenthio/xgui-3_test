using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class OpcodeF6Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xF6;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		if ( reg == 0 ) // TEST r/m8, imm8
		{
			byte imm8 = core.ReadByte( eip + 2 );
			byte value;
			if ( mod == 3 )
			{
				string regName = Get8BitRegisterName( rm );
				value = (byte)(core.Registers[regName] & 0xFF);
			}
			else
			{
				uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				value = core.ReadByte( addr );
			}
			byte result = (byte)(value & imm8);
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = false;
			core.OverflowFlag = false;
			uint len = mod == 3 ? 3u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ) + 1;
			core.Registers["eip"] += len;
		}
		else if ( reg == 2 ) // NOT r/m8
		{
			if ( mod == 3 )
			{
				string regName = Get8BitRegisterName( rm );
				byte value = (byte)(core.Registers[regName] & 0xFF);
				byte result = (byte)~value;
				core.Registers[regName] = (core.Registers[regName] & 0xFFFFFF00) | result;
			}
			else
			{
				uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				byte value = core.ReadByte( addr );
				byte result = (byte)~value;
				core.WriteByte( addr, result );
			}
			uint len = mod == 3 ? 2u : X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
		}
		else
		{
			throw new NotImplementedException( $"Opcode 0xF6 /{reg} not implemented" );
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
