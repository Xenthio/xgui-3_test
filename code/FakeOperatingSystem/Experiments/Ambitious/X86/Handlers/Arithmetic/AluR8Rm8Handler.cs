namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x02 — ADD r8, r/m8
/// 0x0A — OR  r8, r/m8
/// 0x12 — ADC r8, r/m8
/// 0x1A — SBB r8, r/m8
/// 0x22 — AND r8, r/m8
/// 0x2A — SUB r8, r/m8
/// 0x32 — XOR r8, r/m8
/// 0x3A — CMP r8, r/m8
public class AluR8Rm8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0x02 || opcode == 0x0A || opcode == 0x12 || opcode == 0x1A ||
		opcode == 0x22 || opcode == 0x2A || opcode == 0x32 || opcode == 0x3A;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		byte modrm  = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);

		// Source operand (r/m8)
		byte src;
		uint instrLen;
		if ( mod == 3 )
		{
			src = GetReg8( core, rm );
			instrLen = 2;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			src = core.ReadByte( addr );
			instrLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		}

		byte dst = GetReg8( core, reg );
		byte result;
		bool writeBack = true;

		switch ( opcode )
		{
			case 0x02: // ADD r8, r/m8
				result = (byte)(dst + src);
				core.CarryFlag    = (uint)dst + src > 0xFF;
				core.OverflowFlag = (~(dst ^ src) & (dst ^ result) & 0x80) != 0;
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				break;
			case 0x0A: // OR r8, r/m8
				result = (byte)(dst | src);
				core.CarryFlag = core.OverflowFlag = false;
				core.ZeroFlag  = result == 0;
				core.SignFlag  = (result & 0x80) != 0;
				break;
			case 0x12: // ADC r8, r/m8
				{ uint c = core.CarryFlag ? 1u : 0u;
				  uint r = (uint)dst + src + c;
				  result = (byte)r;
				  core.CarryFlag    = r > 0xFF;
				  core.OverflowFlag = (~(dst ^ src) & (dst ^ result) & 0x80) != 0;
				  core.ZeroFlag     = result == 0;
				  core.SignFlag     = (result & 0x80) != 0; }
				break;
			case 0x1A: // SBB r8, r/m8
				{ uint b = core.CarryFlag ? 1u : 0u;
				  uint r = (uint)dst - src - b;
				  result = (byte)r;
				  core.CarryFlag    = r > 0xFF;
				  core.OverflowFlag = ((dst ^ src) & (dst ^ result) & 0x80) != 0;
				  core.ZeroFlag     = result == 0;
				  core.SignFlag     = (result & 0x80) != 0; }
				break;
			case 0x22: // AND r8, r/m8
				result = (byte)(dst & src);
				core.CarryFlag = core.OverflowFlag = false;
				core.ZeroFlag  = result == 0;
				core.SignFlag  = (result & 0x80) != 0;
				break;
			case 0x2A: // SUB r8, r/m8
				result = (byte)(dst - src);
				core.CarryFlag    = dst < src;
				core.OverflowFlag = ((dst ^ src) & (dst ^ result) & 0x80) != 0;
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				break;
			case 0x32: // XOR r8, r/m8
				result = (byte)(dst ^ src);
				core.CarryFlag = core.OverflowFlag = false;
				core.ZeroFlag  = result == 0;
				core.SignFlag  = (result & 0x80) != 0;
				break;
			case 0x3A: // CMP r8, r/m8
				result = (byte)(dst - src);
				core.CarryFlag    = dst < src;
				core.OverflowFlag = ((dst ^ src) & (dst ^ result) & 0x80) != 0;
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				writeBack = false;
				break;
			default:
				result = 0;
				break;
		}

		if ( writeBack )
			SetReg8( core, reg, result );

		core.Registers["eip"] += instrLen;
	}

	private byte GetReg8( X86Core core, byte regCode )
	{
		// 0-3: AL/CL/DL/BL (low byte), 4-7: AH/CH/DH/BH (high byte)
		string regName = regCode switch { 0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
		                                  4 => "eax", 5 => "ecx", 6 => "edx", 7 => "ebx", _ => "eax" };
		return regCode < 4
			? (byte)(core.Registers[regName] & 0xFF)
			: (byte)((core.Registers[regName] >> 8) & 0xFF);
	}

	private void SetReg8( X86Core core, byte regCode, byte value )
	{
		string regName = regCode switch { 0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
		                                  4 => "eax", 5 => "ecx", 6 => "edx", 7 => "ebx", _ => "eax" };
		if ( regCode < 4 )
			core.Registers[regName] = (core.Registers[regName] & 0xFFFFFF00) | value;
		else
			core.Registers[regName] = (core.Registers[regName] & 0xFFFF00FF) | ((uint)value << 8);
	}
}
