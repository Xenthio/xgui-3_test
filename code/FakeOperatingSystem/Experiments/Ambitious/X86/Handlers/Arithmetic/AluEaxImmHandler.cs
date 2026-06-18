namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// Accumulator short-form ALU instructions (missing from elsewhere):
/// 0x0C — OR  AL,  imm8
/// 0x1C — SBB AL,  imm8
/// 0x1D — SBB EAX, imm32
/// 0x2C — SUB AL,  imm8
/// 0x2D — SUB EAX, imm32
public class AluEaxImmHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0x0C ||
		opcode == 0x1C || opcode == 0x1D ||
		opcode == 0x2C || opcode == 0x2D;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		bool wide = (opcode & 1) == 1; // even = AL/imm8, odd = EAX/imm32

		if ( !wide )
		{
			// 8-bit accumulator form (AL)
			byte al  = (byte)(core.Registers["eax"] & 0xFF);
			byte imm = core.ReadByte( eip + 1 );
			byte result;
			bool writeBack = true;

			switch ( opcode )
			{
				case 0x0C: // OR AL, imm8
					result = (byte)(al | imm);
					core.CarryFlag = core.OverflowFlag = false;
					core.ZeroFlag  = result == 0;
					core.SignFlag  = (result & 0x80) != 0;
					break;
				case 0x1C: // SBB AL, imm8
					{ uint b = core.CarryFlag ? 1u : 0u;
					  uint r = (uint)al - imm - b;
					  result = (byte)r;
					  core.CarryFlag    = r > 0xFF;
					  core.OverflowFlag = ((al ^ imm) & (al ^ result) & 0x80) != 0;
					  core.ZeroFlag     = result == 0;
					  core.SignFlag     = (result & 0x80) != 0; }
					break;
				case 0x2C: // SUB AL, imm8
					result = (byte)(al - imm);
					core.CarryFlag    = al < imm;
					core.OverflowFlag = ((al ^ imm) & (al ^ result) & 0x80) != 0;
					core.ZeroFlag     = result == 0;
					core.SignFlag     = (result & 0x80) != 0;
					break;
				default:
					result = al;
					break;
			}

			if ( writeBack )
				core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | result;
			core.Registers["eip"] += 2;
		}
		else
		{
			// 32-bit accumulator form (EAX)
			uint eax = core.Registers["eax"];
			uint imm = core.ReadDword( eip + 1 );
			uint result;
			bool writeBack = true;

			switch ( opcode )
			{
				case 0x1D: // SBB EAX, imm32
					{ uint b = core.CarryFlag ? 1u : 0u;
					  ulong r = (ulong)eax - imm - b;
					  result = (uint)r;
					  core.CarryFlag    = r > 0xFFFFFFFF;
					  core.OverflowFlag = ((eax ^ imm) & (eax ^ result) & 0x80000000) != 0;
					  core.ZeroFlag     = result == 0;
					  core.SignFlag     = (result & 0x80000000) != 0; }
					break;
				case 0x2D: // SUB EAX, imm32
					result = eax - imm;
					core.CarryFlag    = eax < imm;
					core.OverflowFlag = ((eax ^ imm) & (eax ^ result) & 0x80000000) != 0;
					core.ZeroFlag     = result == 0;
					core.SignFlag     = (result & 0x80000000) != 0;
					break;
				default:
					result = eax;
					break;
			}

			if ( writeBack )
				core.Registers["eax"] = result;
			core.Registers["eip"] += 5;
		}
	}
}
