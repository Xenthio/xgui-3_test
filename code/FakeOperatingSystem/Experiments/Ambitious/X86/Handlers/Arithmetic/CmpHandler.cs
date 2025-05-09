using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class CmpHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x39 || opcode == 0x3B;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		uint leftValue, rightValue;
		uint instructionLength = 2; // opcode + modrm

		if ( opcode == 0x39 ) // CMP r/m32, r32
		{
			string regName = GetRegisterName( reg );
			rightValue = core.Registers[regName];

			if ( mod == 3 ) // Register
			{
				string rmName = GetRegisterName( rm );
				leftValue = core.Registers[rmName];
			}
			else // Memory
			{
				uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				leftValue = core.ReadDword( addr );
				instructionLength = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			}
		}
		else // 0x3B: CMP r32, r/m32
		{
			string regName = GetRegisterName( reg );
			leftValue = core.Registers[regName];

			if ( mod == 3 ) // Register
			{
				string rmName = GetRegisterName( rm );
				rightValue = core.Registers[rmName];
			}
			else // Memory
			{
				uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				rightValue = core.ReadDword( addr );
				instructionLength = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			}
		}

		// Calculate comparison
		uint result = leftValue - rightValue;

		// Set flags
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = leftValue < rightValue;

		// Advance EIP
		core.Registers["eip"] += instructionLength;
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
