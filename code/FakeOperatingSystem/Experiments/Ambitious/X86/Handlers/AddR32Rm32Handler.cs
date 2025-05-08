using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class AddR32Rm32Handler : IInstructionHandler
{
	public bool CanHandle(byte opcode) => opcode == 0x03;

	public void Execute(X86Core core)
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte(eip + 1);
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// Get destination register
		string destReg = X86AddressingHelper.GetRegisterName(reg);
		uint destValue = core.Registers[destReg];
		uint sourceValue;

		if (mod == 3) // Register source operand
		{
			string sourceReg = X86AddressingHelper.GetRegisterName(rm);
			sourceValue = core.Registers[sourceReg];
			core.Registers["eip"] += 2;
		}
		else // Memory source operand
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress(core, modrm, eip);
			sourceValue = core.ReadDword(effectiveAddress);
			
			uint length = X86AddressingHelper.GetInstructionLength(modrm);
			core.Registers["eip"] += length;
		}

		// Perform the ADD operation
		uint result = destValue + sourceValue;
		core.Registers[destReg] = result;

		// Set flags
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		
		// Carry flag set if unsigned overflow occurred
		core.CarryFlag = result < destValue; // If result is smaller than original, overflow occurred
		
		// Overflow flag set if signed overflow occurred
		bool destSign = (destValue & 0x80000000) != 0;
		bool sourceSign = (sourceValue & 0x80000000) != 0;
		bool resultSign = (result & 0x80000000) != 0;
		core.OverflowFlag = (destSign == sourceSign) && (resultSign != destSign);
	}
}

