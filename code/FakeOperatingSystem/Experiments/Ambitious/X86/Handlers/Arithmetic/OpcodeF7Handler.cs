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
					{
						// #DE exception: in a real OS this would invoke the divide-error handler.
						// We simulate it by setting EAX=0 EDX=0 and continuing (safe approximation).
						Log.Warning( $"x86 DIV by zero at EIP=0x{eip:X8} (dividend=0x{dividend:X16}) — simulating #DE as EAX=0" );
						core.Registers["eax"] = 0;
						core.Registers["edx"] = 0;
					}
					else
					{
						uint quotient = (uint)(dividend / operand);
						uint remainder = (uint)(dividend % operand);
						// If quotient doesn't fit in 32 bits, raise #DE
						if ( quotient > 0xFFFFFFFF )
						{
							Log.Warning( $"x86 DIV overflow at EIP=0x{eip:X8} — simulating #DE as EAX=0xFFFFFFFF" );
							core.Registers["eax"] = 0xFFFFFFFF;
							core.Registers["edx"] = 0;
						}
						else
						{
							core.Registers["eax"] = quotient;
							core.Registers["edx"] = remainder;
						}
					}
					core.Registers["eip"] += (mod == 3 ? 2u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
				}
				break;
			case 3: // NEG r/m32 (two's complement negate)
				{
					uint negResult = (uint)(-(int)operand);
					core.CarryFlag = operand != 0;
					core.OverflowFlag = operand == 0x80000000;
					core.ZeroFlag = negResult == 0;
					core.SignFlag = (negResult & 0x80000000) != 0;
					if ( mod == 3 )
						core.Registers[X86AddressingHelper.GetRegisterName( rm )] = negResult;
					else
						core.WriteDword( X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip ), negResult );
					core.Registers["eip"] += (mod == 3 ? 2u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
				}
				break;
				case 5: // IMUL r/m32 (signed multiply EAX * operand -> EDX:EAX)
					{
						long imulResult = (long)(int)core.Registers["eax"] * (long)(int)operand;
						core.Registers["eax"] = (uint)(imulResult & 0xFFFFFFFF);
						core.Registers["edx"] = (uint)(imulResult >> 32);
						bool imulOverflow = (int)core.Registers["edx"] != (int)core.Registers["eax"] >> 31;
						core.CarryFlag = imulOverflow;
						core.OverflowFlag = imulOverflow;
						core.Registers["eip"] += (mod == 3 ? 2u : X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
					}
					break;
				case 7: // IDIV r/m32 (signed division EDX:EAX / operand)
					{
						long idivDividend = ((long)(int)core.Registers["edx"] << 32) | core.Registers["eax"];
						int idivDivisor = (int)operand;
						if ( idivDivisor == 0 )
						{
							Log.Warning( $"x86 IDIV by zero at EIP=0x{eip:X8} — simulating #DE as EAX=0" );
							core.Registers["eax"] = 0;
							core.Registers["edx"] = 0;
						}
						else
						{
							long idivQ = idivDividend / idivDivisor;
							long idivR = idivDividend % idivDivisor;
							if ( idivQ > int.MaxValue || idivQ < int.MinValue )
							{
								Log.Warning( $"x86 IDIV overflow at EIP=0x{eip:X8} — simulating #DE" );
								core.Registers["eax"] = 0;
								core.Registers["edx"] = 0;
							}
							else
							{
								core.Registers["eax"] = (uint)(int)idivQ;
								core.Registers["edx"] = (uint)(int)idivR;
							}
						}
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
