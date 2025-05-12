using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class OrR32Rm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x0B;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = GetRegisterName( reg );
		uint result;

		if ( mod == 3 ) // Register-to-register
		{
			string sourceReg = GetRegisterName( rm );
			uint sourceValue = core.Registers[sourceReg];
			result = core.Registers[destReg] | sourceValue;
			core.LogMaths( $"OR {destReg}, {sourceReg}, result: {result} (EIP: {eip:X8})" );
		}
		else // Memory source operand
		{
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint sourceValue = core.ReadDword( effectiveAddress );
			result = core.Registers[destReg] | sourceValue;
			core.LogMaths( $"OR {destReg}, [0x{effectiveAddress:X8}], result: {result} (EIP: {eip:X8})" );
		}

		core.Registers[destReg] = result;

		// Set flags
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = false;
		core.OverflowFlag = false;

		// Advance EIP
		if ( mod == 3 )
			core.Registers["eip"] += 2;
		else
		{
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length;
		}
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
