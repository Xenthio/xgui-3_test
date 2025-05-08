namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovR8Rm8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x8A;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// Get destination register
		string destRegFull = X86AddressingHelper.GetRegisterName( reg );
		uint destValueFull = core.Registers[destRegFull];

		byte sourceValue;

		if ( mod == 3 ) // Register to register
		{
			string sourceRegFull = X86AddressingHelper.GetRegisterName( rm );
			uint sourceValueFull = core.Registers[sourceRegFull];
			sourceValue = (byte)(sourceValueFull & 0xFF);

			core.Registers["eip"] += 2;
		}
		else // Memory source
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );

			// Read a single byte from memory
			sourceValue = core.ReadByte( effectiveAddress );

			// Advance EIP
			uint length = X86AddressingHelper.GetInstructionLength( modrm );
			core.Registers["eip"] += length;
		}

		// Update destination register, preserving high bits
		core.Registers[destRegFull] = (destValueFull & 0xFFFFFF00) | sourceValue;
	}
}
