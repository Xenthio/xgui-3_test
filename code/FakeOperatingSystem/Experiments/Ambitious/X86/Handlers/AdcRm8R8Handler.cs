namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class AdcRm8R8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x10;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// Get source register value (8-bit)
		string sourceRegFull = X86AddressingHelper.GetRegisterName( reg );
		uint sourceValueFull = core.Registers[sourceRegFull];
		byte sourceValue = (byte)(sourceValueFull & 0xFF);

		// Add the carry flag
		uint carryValue = core.CarryFlag ? 1u : 0u;

		if ( mod == 3 ) // Register destination
		{
			// Get destination register
			string destRegFull = X86AddressingHelper.GetRegisterName( rm );
			uint destValueFull = core.Registers[destRegFull];
			byte destValue = (byte)(destValueFull & 0xFF);

			// Perform ADC operation
			uint tempResult = (uint)destValue + (uint)sourceValue + carryValue;
			byte result = (byte)(tempResult & 0xFF);

			// Update register (preserving high bytes)
			core.Registers[destRegFull] = (destValueFull & 0xFFFFFF00) | result;

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = tempResult > 0xFF;

			// Overflow occurs when the sign of both inputs is the same and different from the result
			bool destSign = (destValue & 0x80) != 0;
			bool sourceSign = (sourceValue & 0x80) != 0;
			bool resultSign = (result & 0x80) != 0;
			core.OverflowFlag = (destSign == sourceSign) && (destSign != resultSign);

			core.Registers["eip"] += 2;
		}
		else // Memory destination
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			byte destValue = core.ReadByte( effectiveAddress );

			// Perform ADC operation
			uint tempResult = (uint)destValue + (uint)sourceValue + carryValue;
			byte result = (byte)(tempResult & 0xFF);

			// Write result back to memory
			core.WriteByte( effectiveAddress, result );

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = tempResult > 0xFF;

			// Overflow calculation
			bool destSign = (destValue & 0x80) != 0;
			bool sourceSign = (sourceValue & 0x80) != 0;
			bool resultSign = (result & 0x80) != 0;
			core.OverflowFlag = (destSign == sourceSign) && (destSign != resultSign);

			// Advance EIP
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length;
		}
	}
}

