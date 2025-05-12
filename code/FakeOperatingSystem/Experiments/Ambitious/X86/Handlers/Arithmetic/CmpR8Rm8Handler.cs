using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class CmpR8Rm8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x3A;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string regName = Get8BitRegisterName( reg );
		byte regValue = (byte)(core.Registers[regName] & 0xFF);

		byte value;
		if ( mod == 3 )
		{
			string srcReg = Get8BitRegisterName( rm );
			value = (byte)(core.Registers[srcReg] & 0xFF);
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			value = core.ReadByte( addr );
		}

		int result = regValue - value;

		// Set flags (simplified)
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80) != 0;
		core.CarryFlag = regValue < value;
		core.OverflowFlag = ((regValue ^ value) & (regValue ^ result) & 0x80) != 0;

		core.Registers["eip"] += 2; // opcode + modrm
	}

	private string Get8BitRegisterName( int code ) => code switch
	{
		0 => "eax", // AL
		1 => "ecx", // CL
		2 => "edx", // DL
		3 => "ebx", // BL
		4 => "eax", // AH
		5 => "ecx", // CH
		6 => "edx", // DH
		7 => "ebx", // BH
		_ => throw new ArgumentException( $"Invalid 8-bit register code: {code}" )
	};
}
