namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class SubRm32R32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x29;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// Get source register value
		string sourceReg = X86AddressingHelper.GetRegisterName( reg );
		uint sourceValue = core.Registers[sourceReg];

		if ( mod == 3 ) // Register to register
		{
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			uint destValue = core.Registers[destReg];

			// Perform subtraction
			uint result = destValue - sourceValue;
			core.Registers[destReg] = result;

			// Set flags
			SetFlags( core, destValue, sourceValue, result );

			core.Registers["eip"] += 2;
		}
		else // Memory destination
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint destValue = core.ReadDword( effectiveAddress );

			// Perform subtraction
			uint result = destValue - sourceValue;
			core.WriteDword( effectiveAddress, result );

			// Set flags
			SetFlags( core, destValue, sourceValue, result );

			// Advance EIP
			uint length = X86AddressingHelper.GetInstructionLength( modrm );
			core.Registers["eip"] += length;
		}
	}

	private void SetFlags( X86Core core, uint destValue, uint sourceValue, uint result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = destValue < sourceValue; // Borrow occurred

		// Overflow occurs when sign of source and dest differ and sign of dest and result differ
		bool destSign = (destValue & 0x80000000) != 0;
		bool sourceSign = (sourceValue & 0x80000000) != 0;
		bool resultSign = (result & 0x80000000) != 0;
		core.OverflowFlag = (destSign != sourceSign) && (destSign != resultSign);
	}
}
