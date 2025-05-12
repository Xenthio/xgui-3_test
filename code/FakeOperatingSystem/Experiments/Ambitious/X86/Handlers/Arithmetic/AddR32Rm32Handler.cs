namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class AddR32Rm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x03;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );
		uint destValue = core.Registers[destReg];
		uint sourceValue;
		string logMsg;

		if ( mod == 3 ) // Register source operand
		{
			string sourceReg = X86AddressingHelper.GetRegisterName( rm );
			sourceValue = core.Registers[sourceReg];
			core.Registers["eip"] += 2;
			logMsg = $"ADD {destReg}, {sourceReg}, {destValue} + {sourceValue} = ";
		}
		else // Memory source operand
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			sourceValue = core.ReadDword( effectiveAddress );
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length;
			logMsg = $"ADD {destReg}, [0x{effectiveAddress:X8}], {destValue} + {sourceValue} = ";
		}

		// Perform the ADD operation
		uint result = destValue + sourceValue;
		core.Registers[destReg] = result;

		// Set flags
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = result < destValue;
		bool destSign = (destValue & 0x80000000) != 0;
		bool sourceSign = (sourceValue & 0x80000000) != 0;
		bool resultSign = (result & 0x80000000) != 0;
		core.OverflowFlag = (destSign == sourceSign) && (resultSign != destSign);

		// Log the operation
		core.LogMaths( $"{logMsg}{result} (EIP: {eip:X8})" );
	}
}

