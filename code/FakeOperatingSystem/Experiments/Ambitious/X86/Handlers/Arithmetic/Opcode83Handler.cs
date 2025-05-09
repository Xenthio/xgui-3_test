using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class Opcode83Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x83;

	public void Execute( X86Core core )
	{
		core.LogVerbose( $"Opcode83Handler: opcode=0x83" );
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7); // Operation type
		byte rm = (byte)(modrm & 0x7);

		// This is like 0x81 but with sign-extended 8-bit immediate
		sbyte imm8 = (sbyte)core.ReadByte( eip + 2 );
		int signExtImm = imm8; // Automatic sign extension to int

		if ( mod == 3 ) // Register operand
		{
			string destReg = GetRegisterName( rm );
			uint value = core.Registers[destReg];

			switch ( reg )
			{
				case 0: // ADD
					core.Registers[destReg] = value + (uint)signExtImm;
					core.LogVerbose( $"Add {destReg}, {signExtImm:X8} = {core.Registers[destReg]:X8}" );
					break;
				case 1: // OR
					core.Registers[destReg] = value | (uint)signExtImm;
					core.LogVerbose( $"Or {destReg}, {signExtImm:X8} = {core.Registers[destReg]:X8}" );
					break;
				case 4: // AND
					core.Registers[destReg] = value & (uint)signExtImm;
					core.LogVerbose( $"And {destReg}, {signExtImm:X8} = {core.Registers[destReg]:X8}" );
					break;
				case 5: // SUB
					core.Registers[destReg] = value - (uint)signExtImm;
					core.LogVerbose( $"Sub {destReg}, {signExtImm:X8} = {core.Registers[destReg]:X8}" );
					break;
				case 6: // XOR
					core.Registers[destReg] = value ^ (uint)signExtImm;
					core.LogVerbose( $"Xor {destReg}, {signExtImm:X8} = {core.Registers[destReg]:X8}" );
					break;
				case 7: // CMP
					uint result = value - (uint)signExtImm;
					core.ZeroFlag = result == 0;
					core.SignFlag = (result & 0x80000000) != 0;
					core.CarryFlag = value < (uint)signExtImm;
					break;
				default:
					throw new NotImplementedException( $"Opcode 0x83 with reg={reg} not implemented" );
			}
			core.Registers["eip"] += 3;
		}
		else // Memory operand
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint value = core.ReadDword( addr );

			switch ( reg )
			{
				case 0: // ADD
					core.WriteDword( addr, value + (uint)signExtImm );
					core.LogVerbose( $"Add [0x{addr:X8}], {signExtImm:X8} = {core.ReadDword( addr ):X8}" );
					break;
				case 1: // OR
					core.WriteDword( addr, value | (uint)signExtImm );
					core.LogVerbose( $"Or [0x{addr:X8}], {signExtImm:X8} = {core.ReadDword( addr ):X8}" );
					break;
				case 4: // AND
					core.WriteDword( addr, value & (uint)signExtImm );
					core.LogVerbose( $"And [0x{addr:X8}], {signExtImm:X8} = {core.ReadDword( addr ):X8}" );
					break;
				case 5: // SUB
					core.WriteDword( addr, value - (uint)signExtImm );
					core.LogVerbose( $"Sub [0x{addr:X8}], {signExtImm:X8} = {core.ReadDword( addr ):X8}" );
					break;
				case 6: // XOR
					core.WriteDword( addr, value ^ (uint)signExtImm );
					core.LogVerbose( $"Xor [0x{addr:X8}], {signExtImm:X8} = {core.ReadDword( addr ):X8}" );
					break;
				case 7: // CMP
					uint result = value - (uint)signExtImm;
					core.ZeroFlag = result == 0;
					core.SignFlag = (result & 0x80000000) != 0;
					core.CarryFlag = value < (uint)signExtImm;
					break;
				default:
					throw new NotImplementedException( $"Opcode 0x83 with reg={reg} not implemented" );
			}
			// Advance EIP by the correct instruction length
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
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
