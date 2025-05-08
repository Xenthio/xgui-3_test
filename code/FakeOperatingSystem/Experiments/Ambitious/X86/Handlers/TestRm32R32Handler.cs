using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class TestRm32R32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x85;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string sourceReg = X86AddressingHelper.GetRegisterName( reg );
		uint sourceValue = core.Registers[sourceReg];
		uint destValue;

		if ( mod == 3 ) // Register operand
		{
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			destValue = core.Registers[destReg];
			core.Registers["eip"] += 2;
		}
		else // Memory operand
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			destValue = core.ReadDword( effectiveAddress );

			// Calculate instruction length based on addressing mode
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length;
		}

		// Perform the TEST operation (bitwise AND but don't store result)
		uint result = sourceValue & destValue;

		// Set flags based on result
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = false; // Always cleared
		core.OverflowFlag = false; // Always cleared
	}

	private string GetRegisterName( int code ) => code switch
	{
		0 => "eax",
		1 => "ecx",
		2 => "edx",
		3 => "ebx",
		4 => "esp",
		5 => "ebp",
		6 => "esi",
		7 => "edi",
		_ => throw new ArgumentException( $"Invalid register code: {code}" )
	};
}
