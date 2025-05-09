using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class Opcode81Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x81;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7); // This determines the operation
		byte rm = (byte)(modrm & 0x7);

		uint value = 0;

		if ( mod == 3 ) // Register operand
		{
			string regName = GetRegisterName( rm );
			value = core.Registers[regName];
		}
		else
		{
			// Memory operand - not implemented in this basic handler
			throw new NotImplementedException( $"Memory operand not implemented for opcode 0x81 with mod={mod}" );
		}

		// Read the immediate value (32-bit)
		uint imm32 = core.ReadDword( eip + 2 );

		// Perform the operation based on reg field
		switch ( reg )
		{
			case 0: // ADD
				if ( mod == 3 )
				{
					string regName = GetRegisterName( rm );
					core.Registers[regName] += imm32;
					core.LogVerbose( $"ADD {regName}, {imm32:X8} = {core.Registers[regName]:X8}" );
				}
				break;

			case 1: // OR
				if ( mod == 3 )
				{
					string regName = GetRegisterName( rm );
					core.Registers[regName] |= imm32;
					core.LogVerbose( $"OR {regName}, {imm32:X8} = {core.Registers[regName]:X8}" );
				}
				break;

			case 4: // AND
				if ( mod == 3 )
				{
					string regName = GetRegisterName( rm );
					core.Registers[regName] &= imm32;
					core.LogVerbose( $"AND {regName}, {imm32:X8} = {core.Registers[regName]:X8}" );
				}
				break;

			case 5: // SUB
				if ( mod == 3 )
				{
					string regName = GetRegisterName( rm );
					core.Registers[regName] -= imm32;
					core.LogVerbose( $"SUB {regName}, {imm32:X8} = {core.Registers[regName]:X8}" );
				}
				break;

			case 6: // XOR
				if ( mod == 3 )
				{
					string regName = GetRegisterName( rm );
					core.Registers[regName] ^= imm32;
					core.LogVerbose( $"XOR {regName}, {imm32:X8} = {core.Registers[regName]:X8}" );
				}
				break;

			case 7: // CMP
				if ( mod == 3 )
				{
					string regName = GetRegisterName( rm );
					uint result = core.Registers[regName] - imm32;
					core.ZeroFlag = result == 0;
					core.SignFlag = (result & 0x80000000) != 0;
					core.CarryFlag = core.Registers[regName] < imm32;
					// Overflow flag calculation is more complex; simplified here
				}
				break;

			default:
				throw new NotImplementedException( $"Operation {reg} not implemented for opcode 0x81" );
		}

		// Advance EIP past the instruction
		core.Registers["eip"] += 6; // 1 byte opcode + 1 byte modrm + 4 bytes immediate
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
