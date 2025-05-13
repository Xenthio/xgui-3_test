using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class OrRm32R32Handler : IInstructionHandler
{
	public bool CanHandle(byte opcode) => opcode == 0x09;

	public void Execute(X86Core core)
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte(eip + 1);

		int mod = (modrm >> 6) & 0x3;
		int reg = (modrm >> 3) & 0x7;
		int rm = modrm & 0x7;

		// Get source value (r32)
		uint srcVal = GetRegister32(core, reg);

		// Get destination value (r/m32)
		uint destVal;
		uint destAddr = 0;
		bool isReg = false;

		if (mod == 3)
		{
			// Register-direct mode
			destVal = GetRegister32(core, rm);
			isReg = true;
		}
		else
		{
			// Memory mode, use X86AddressingHelper
			destAddr = X86AddressingHelper.CalculateEffectiveAddress(core, modrm, eip);
			destVal = core.ReadDword(destAddr);
		}

		uint result = destVal | srcVal;

		// Write result back
		if (isReg)
			SetRegister32(core, rm, result);
		else
			core.WriteDword(destAddr, result);

		// Set flags (ZF, SF, PF, CF=0, OF=0)
		core.ZeroFlag = (result == 0);
		core.SignFlag = (result & 0x80000000) != 0;
		core.ParityFlag = CalculateParity((byte)result);
		core.CarryFlag = false;
		core.OverflowFlag = false;

		// Advance EIP using helper for correct instruction length
		core.Registers["eip"] += X86AddressingHelper.GetInstructionLength(modrm, core, eip);

		if (X86Core.VerboseLogging)
		{
			core.LogVerbose($"OR r/m32, r32: 0x{destVal:X8} | 0x{srcVal:X8} = 0x{result:X8}");
		}
	}

	private uint GetRegister32(X86Core core, int index)
	{
		return index switch
		{
			0 => core.Registers["eax"],
			1 => core.Registers["ecx"],
			2 => core.Registers["edx"],
			3 => core.Registers["ebx"],
			4 => core.Registers["esp"],
			5 => core.Registers["ebp"],
			6 => core.Registers["esi"],
			7 => core.Registers["edi"],
			_ => throw new ArgumentOutOfRangeException(nameof(index)),
		};
	}

	private void SetRegister32(X86Core core, int index, uint value)
	{
		switch (index)
		{
			case 0: core.Registers["eax"] = value; break;
			case 1: core.Registers["ecx"] = value; break;
			case 2: core.Registers["edx"] = value; break;
			case 3: core.Registers["ebx"] = value; break;
			case 4: core.Registers["esp"] = value; break;
			case 5: core.Registers["ebp"] = value; break;
			case 6: core.Registers["esi"] = value; break;
			case 7: core.Registers["edi"] = value; break;
			default: throw new ArgumentOutOfRangeException(nameof(index));
		}
	}

	private bool CalculateParity(byte value)
	{
		int count = 0;
		for (int i = 0; i < 8; i++)
			if (((value >> i) & 1) != 0) count++;
		return (count % 2) == 0;
	}
}