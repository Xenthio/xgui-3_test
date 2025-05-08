namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class AddRm32R32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x01;

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

		if ( mod == 3 ) // Register destination
		{
			// Get destination register
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			uint destValue = core.Registers[destReg];

			// Perform addition
			uint result = destValue + sourceValue;
			core.Registers[destReg] = result;

			// Set flags
			SetFlags( core, destValue, sourceValue, result );

			core.Registers["eip"] += 2;
		}
		else // Memory destination
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint destValue = core.ReadDword( effectiveAddress );

			// Perform addition
			uint result = destValue + sourceValue;
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
		// Zero Flag (ZF): Set if the result is zero
		core.ZeroFlag = result == 0;

		// Sign Flag (SF): Set if the most significant bit of the result is 1
		core.SignFlag = (result & 0x80000000) != 0;

		// Carry Flag (CF): Set if an unsigned overflow occurred
		core.CarryFlag = result < destValue; // If result is less than original, carry occurred

		// Overflow Flag (OF): Set if a signed overflow occurred
		bool destSign = (destValue & 0x80000000) != 0;
		bool sourceSign = (sourceValue & 0x80000000) != 0;
		bool resultSign = (result & 0x80000000) != 0;
		core.OverflowFlag = (destSign == sourceSign) && (destSign != resultSign);
	}
}
