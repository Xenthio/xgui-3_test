namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovRm8R8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x88;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// Get source register and value (8-bit)
		string sourceRegFull = X86AddressingHelper.GetRegisterName( reg );
		uint sourceValueFull = core.Registers[sourceRegFull];
		byte sourceValue = (byte)(sourceValueFull & 0xFF);

		if ( mod == 3 ) // Register to register
		{
			// Destination register
			string destRegFull = X86AddressingHelper.GetRegisterName( rm );

			// For 8-bit register access, preserve the high bits
			uint destValueFull = core.Registers[destRegFull];
			core.Registers[destRegFull] = (destValueFull & 0xFFFFFF00) | sourceValue;

			core.Registers["eip"] += 2;
		}
		else // Memory destination
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );

			// Write a single byte to memory
			core.WriteByte( effectiveAddress, sourceValue );

			// Advance EIP
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length;
		}
	}
}
