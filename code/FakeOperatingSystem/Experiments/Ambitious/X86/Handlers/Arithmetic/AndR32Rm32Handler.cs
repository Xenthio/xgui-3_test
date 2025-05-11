namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class AndR32Rm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x23;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );

		if ( mod == 3 ) // Register to register
		{
			string srcReg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[destReg] &= core.Registers[srcReg];
			core.Registers["eip"] += 2;
		}
		else // Memory to register
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			core.Registers[destReg] &= core.ReadDword( effectiveAddress );
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length;
		}

		// Set flags (simplified)
		uint result = core.Registers[destReg];
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = false;
		core.OverflowFlag = false;
	}
}
