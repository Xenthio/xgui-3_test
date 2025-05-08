using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class XorHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x33;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = GetRegisterName( reg );

		if ( mod == 3 ) // Register-to-register XOR
		{
			string sourceReg = GetRegisterName( rm );
			uint result = core.Registers[destReg] ^ core.Registers[sourceReg];
			core.Registers[destReg] = result;

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80000000) != 0;
			core.CarryFlag = false; // XOR always clears CF
			core.OverflowFlag = false; // XOR always clears OF

			core.Registers["eip"] += 2;
		}
		else // Memory source operand
		{
			uint effectiveAddress = CalculateEffectiveAddress( core, modrm, eip );
			uint sourceValue = core.ReadDword( effectiveAddress );
			uint result = core.Registers[destReg] ^ sourceValue;
			core.Registers[destReg] = result;

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80000000) != 0;
			core.CarryFlag = false;
			core.OverflowFlag = false;

			// Advance EIP based on addressing mode
			core.Registers["eip"] += GetInstructionLength( mod, rm );
		}
	}

	private uint CalculateEffectiveAddress( X86Core core, byte modrm, uint eip )
	{
		byte mod = (byte)(modrm >> 6);
		byte rm = (byte)(modrm & 0x7);

		if ( mod == 0 && rm == 5 ) // [disp32]
			return core.ReadDword( eip + 2 );

		uint ea = 0;

		// Base register
		if ( rm != 4 ) // Not SIB
			ea = core.Registers[GetRegisterName( rm )];
		else
			throw new NotImplementedException( "XorHandler: SIB addressing not implemented" );

		// Displacement
		if ( mod == 1 ) // 8-bit displacement
			ea += (uint)(sbyte)core.ReadByte( eip + 2 );
		else if ( mod == 2 ) // 32-bit displacement
			ea += core.ReadDword( eip + 2 );

		return ea;
	}

	private uint GetInstructionLength( byte mod, byte rm )
	{
		if ( mod == 0 && rm == 5 ) // [disp32]
			return 6;
		else if ( mod == 0 ) // [reg]
			return 2;
		else if ( mod == 1 ) // [reg+disp8]
			return 3;
		else if ( mod == 2 ) // [reg+disp32]
			return 6;
		else // mod == 3, register to register
			return 2;
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
