using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovRmR32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x89;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string sourceReg = GetRegisterName( reg );
		uint value = core.Registers[sourceReg];

		if ( mod == 3 ) // Register to register
		{
			string destReg = GetRegisterName( rm );
			core.Registers[destReg] = value;
			core.Registers["eip"] += 2;
		}
		else // Memory destination (all addressing modes, including SIB)
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			core.WriteDword( effectiveAddress, value );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
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
