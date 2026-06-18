using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x80 — Group 1: ALU r/m8, imm8 (ADD/OR/ADC/SBB/AND/SUB/XOR/CMP)
public class Opcode80Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x80;

	public void Execute( X86Core core )
	{
		uint eip    = core.Registers["eip"];
		byte modrm  = core.ReadByte( eip + 1 );
		byte mod    = (byte)(modrm >> 6);
		byte reg    = (byte)((modrm >> 3) & 0x7);
		byte rm     = (byte)(modrm & 0x7);

		if ( mod == 3 )
		{
			// Register operand
			string destReg = Get8BitRegisterName( rm );
			byte value     = GetReg8( core, rm );
			byte imm8      = core.ReadByte( eip + 2 );
			byte result    = Compute( core, reg, value, imm8 );
			if ( reg != 7 ) // CMP doesn't write back
				SetReg8( core, rm, result );
			SetFlags8( core, reg, value, imm8, result );
			core.Registers["eip"] += 3;
		}
		else
		{
			// Memory operand
			uint addr  = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint len   = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			byte value = core.ReadByte( addr );
			byte imm8  = core.ReadByte( eip + len );
			byte result = Compute( core, reg, value, imm8 );
			if ( reg != 7 )
				core.WriteByte( addr, result );
			SetFlags8( core, reg, value, imm8, result );
			core.Registers["eip"] += len + 1;
		}
	}

	private static byte Compute( X86Core core, byte op, byte a, byte b )
	{
		return op switch
		{
			0 => (byte)(a + b),
			1 => (byte)(a | b),
			2 => (byte)((uint)a + b + (core.CarryFlag ? 1u : 0u)),
			3 => (byte)((uint)a - b - (core.CarryFlag ? 1u : 0u)),
			4 => (byte)(a & b),
			5 => (byte)(a - b),
			6 => (byte)(a ^ b),
			7 => (byte)(a - b), // CMP
			_ => throw new NotImplementedException( $"0x80 /{op} not defined" )
		};
	}

	private static void SetFlags8( X86Core core, byte op, byte a, byte b, byte result )
	{
		switch ( op )
		{
			case 0: // ADD
				core.CarryFlag    = (uint)a + b > 0xFF;
				core.OverflowFlag = (~(a ^ b) & (a ^ result) & 0x80) != 0;
				break;
			case 1: case 6: // OR / XOR
				core.CarryFlag = core.OverflowFlag = false;
				break;
			case 2: // ADC
				{ uint c = core.CarryFlag ? 1u : 0u;
				  core.CarryFlag    = (uint)a + b + c > 0xFF;
				  core.OverflowFlag = (~(a ^ b) & (a ^ result) & 0x80) != 0; }
				break;
			case 3: // SBB
				{ uint borrow = core.CarryFlag ? 1u : 0u;
				  core.CarryFlag    = (uint)a < (uint)b + borrow;
				  core.OverflowFlag = ((a ^ b) & (a ^ result) & 0x80) != 0; }
				break;
			case 4: // AND
				core.CarryFlag = core.OverflowFlag = false;
				break;
			case 5: case 7: // SUB / CMP
				core.CarryFlag    = a < b;
				core.OverflowFlag = ((a ^ b) & (a ^ result) & 0x80) != 0;
				break;
		}
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80) != 0;
	}

	private static byte GetReg8( X86Core core, byte code )
	{
		string r = code switch { 0=>"eax",1=>"ecx",2=>"edx",3=>"ebx",4=>"eax",5=>"ecx",6=>"edx",7=>"ebx",_=>"eax" };
		return code < 4 ? (byte)(core.Registers[r] & 0xFF) : (byte)((core.Registers[r] >> 8) & 0xFF);
	}
	private static void SetReg8( X86Core core, byte code, byte val )
	{
		string r = code switch { 0=>"eax",1=>"ecx",2=>"edx",3=>"ebx",4=>"eax",5=>"ecx",6=>"edx",7=>"ebx",_=>"eax" };
		if ( code < 4 ) core.Registers[r] = (core.Registers[r] & 0xFFFFFF00) | val;
		else            core.Registers[r] = (core.Registers[r] & 0xFFFF00FF) | ((uint)val << 8);
	}
	private static string Get8BitRegisterName( byte code ) =>
		code switch { 0=>"eax",1=>"ecx",2=>"edx",3=>"ebx",4=>"eax",5=>"ecx",6=>"edx",7=>"ebx",_=>"eax" };
}
