using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class OpcodeF7Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xF7;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		uint operand;
		if ( mod == 3 )
		{
			string regName = X86AddressingHelper.GetRegisterName( rm );
			operand = core.Registers[regName];
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			operand = core.ReadDword( addr );
		}

		switch ( reg )
		{
			case 0: // TEST r/m32, imm32
				uint imm32 = core.ReadDword( eip + (mod == 3 ? 2 : X86AddressingHelper.GetInstructionLength( modrm, core, eip )) );
				uint result = operand & imm32;
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80000000) != 0;
				core.CarryFlag = false;
				core.OverflowFlag = false;
				core.Registers["eip"] += (mod == 3 ? 6u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ) + 4u);
				break;
			case 2: // NOT r/m32
				if ( mod == 3 )
				{
					string regName = X86AddressingHelper.GetRegisterName( rm );
					core.Registers[regName] = ~operand;
				}
				else
				{
					uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
					core.WriteDword( addr, ~operand );
				}
				core.Registers["eip"] += (mod == 3 ? 2u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
				break;
			case 6: // DIV r/m32 (unsigned)
				{
					ulong dividend = ((ulong)core.Registers["edx"] << 32) | core.Registers["eax"];
					if ( operand == 0 )
						throw new DivideByZeroException( "x86 DIV: Division by zero" );
					uint quotient = (uint)(dividend / operand);
					uint remainder = (uint)(dividend % operand);
					// If quotient doesn't fit in 32 bits, raise #DE
					if ( quotient > 0xFFFFFFFF )
						throw new Exception( "x86 DIV: Quotient overflow" );
					core.Registers["eax"] = quotient;
					core.Registers["edx"] = remainder;
					core.Registers["eip"] += (mod == 3 ? 2u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
				}
				break;
			case 3: // IDIV r/m32 (signed division)
				{
					long dividend = ((long)((int)core.Registers["edx"]) << 32) | (uint)core.Registers["eax"];
					int divisor = (int)operand;
					if ( divisor == 0 )
						throw new DivideByZeroException( "x86 IDIV: Division by zero" );
					int quotient = (int)(dividend / divisor);
					int remainder = (int)(dividend % divisor);
					// If quotient doesn't fit in 32 bits, raise #DE
					if ( quotient > int.MaxValue || quotient < int.MinValue )
						throw new Exception( "x86 IDIV: Quotient overflow" );
					core.Registers["eax"] = (uint)quotient;
					core.Registers["edx"] = (uint)remainder;
					core.Registers["eip"] += (mod == 3 ? 2u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
				}
				break;
			case 4: // MUL r/m32 (unsigned multiply)
				{
					ulong result2 = (ulong)core.Registers["eax"] * (ulong)operand;
					core.Registers["eax"] = (uint)(result2 & 0xFFFFFFFF);
					core.Registers["edx"] = (uint)(result2 >> 32);

					// Set flags: CF and OF are set if upper 32 bits of result are nonzero
					bool overflow = core.Registers["edx"] != 0;
					core.CarryFlag = overflow;
					core.OverflowFlag = overflow;

					core.Registers["eip"] += (mod == 3 ? 2u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
					break;
				}
			default:
				throw new NotImplementedException( $"Opcode 0xF7 with reg={reg} not implemented" );
		}
	}
}
