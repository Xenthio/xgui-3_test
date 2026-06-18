using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0xF6 — Unary Group 3: TEST/NOT/NEG/MUL/IMUL/DIV/IDIV r/m8
public class OpcodeF6Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xF6;

	public void Execute( X86Core core )
	{
		uint eip   = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod   = (byte)(modrm >> 6);
		byte reg   = (byte)((modrm >> 3) & 0x7);
		byte rm    = (byte)(modrm & 0x7);

		byte operand;
		uint instrLen;
		uint addr = 0;
		if ( mod == 3 )
		{
			operand   = GetReg8( core, rm );
			instrLen  = 2;
		}
		else
		{
			addr      = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			operand   = core.ReadByte( addr );
			instrLen  = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		}

		switch ( reg )
		{
			case 0: // TEST r/m8, imm8
			{
				byte imm8  = core.ReadByte( eip + instrLen );
				byte result = (byte)(operand & imm8);
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				core.CarryFlag    = false;
				core.OverflowFlag = false;
				core.Registers["eip"] += instrLen + 1;
				break;
			}
			case 2: // NOT r/m8
			{
				byte result = (byte)~operand;
				if ( mod == 3 ) SetReg8( core, rm, result );
				else            core.WriteByte( addr, result );
				core.Registers["eip"] += instrLen;
				break;
			}
			case 3: // NEG r/m8
			{
				byte result = (byte)(-(sbyte)operand);
				core.CarryFlag    = operand != 0;
				core.OverflowFlag = operand == 0x80;
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				if ( mod == 3 ) SetReg8( core, rm, result );
				else            core.WriteByte( addr, result );
				core.Registers["eip"] += instrLen;
				break;
			}
			case 4: // MUL r/m8 — AX = AL * r/m8 (unsigned)
			{
				uint ax = (core.Registers["eax"] & 0xFF) * (uint)operand;
				core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | (ax & 0xFFFF);
				core.CarryFlag    = (ax >> 8) != 0;
				core.OverflowFlag = core.CarryFlag;
				core.Registers["eip"] += instrLen;
				break;
			}
			case 5: // IMUL r/m8 — AX = AL * r/m8 (signed)
			{
				short ax = (short)((sbyte)(core.Registers["eax"] & 0xFF) * (sbyte)operand);
				core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | (uint)(ushort)ax;
				bool overflow = (sbyte)(ax & 0xFF) != ax;
				core.CarryFlag    = overflow;
				core.OverflowFlag = overflow;
				core.Registers["eip"] += instrLen;
				break;
			}
			case 6: // DIV r/m8 — AL = AX/r/m8, AH = AX%r/m8 (unsigned)
			{
				if ( operand == 0 ) throw new DivideByZeroException( "x86 DIV8: Division by zero" );
				ushort ax = (ushort)(core.Registers["eax"] & 0xFFFF);
				uint q = (uint)(ax / operand);
				uint r = (uint)(ax % operand);
				if ( q > 0xFF ) throw new Exception( "x86 DIV8: Quotient overflow" );
				core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | (r << 8) | q;
				core.Registers["eip"] += instrLen;
				break;
			}
			case 7: // IDIV r/m8 — AL = AX/r/m8, AH = AX%r/m8 (signed)
			{
				if ( operand == 0 ) throw new DivideByZeroException( "x86 IDIV8: Division by zero" );
				short ax = (short)(core.Registers["eax"] & 0xFFFF);
				int q = ax / (sbyte)operand;
				int r = ax % (sbyte)operand;
				if ( q > 127 || q < -128 ) throw new Exception( "x86 IDIV8: Quotient overflow" );
				core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | ((uint)(byte)(sbyte)r << 8) | (uint)(byte)(sbyte)q;
				core.Registers["eip"] += instrLen;
				break;
			}
			default:
				throw new NotImplementedException( $"Opcode 0xF6 /{reg} not implemented" );
		}
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
}
