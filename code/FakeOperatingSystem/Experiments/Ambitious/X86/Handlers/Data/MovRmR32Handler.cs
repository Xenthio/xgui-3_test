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

		string sourceReg = X86AddressingHelper.GetRegisterName( reg );
		uint value = core.Registers[sourceReg];

		if ( mod == 3 ) // Register to register
		{
			string destReg = X86AddressingHelper.GetRegisterName( rm );
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


}
