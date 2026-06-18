namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x08 — OR  r/m8, r8
/// 0x18 — SBB r/m8, r8
/// 0x28 — SUB r/m8, r8
/// 0x38 — CMP r/m8, r8   (does not write back)
public class AluRm8R8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0x08 || opcode == 0x18 || opcode == 0x28 || opcode == 0x38;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		byte modrm  = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);

		// Source: register (r8)
		byte src = GetReg8( core, reg );

		// Destination: r/m8
		byte dst;
		uint instrLen;
		uint destAddr = 0;

		if ( mod == 3 )
		{
			dst = GetReg8( core, rm );
			instrLen = 2;
		}
		else
		{
			destAddr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			dst = core.ReadByte( destAddr );
			instrLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		}

		byte result;
		bool writeBack = true;

		switch ( opcode )
		{
			case 0x00: // ADD r/m8, r8
				result = (byte)(dst + src);
				core.CarryFlag    = (uint)dst + src > 0xFF;
				core.OverflowFlag = (~(dst ^ src) & (dst ^ result) & 0x80) != 0;
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				break;
			case 0x08: // OR r/m8, r8
				result = (byte)(dst | src);
				core.CarryFlag = core.OverflowFlag = false;
				core.ZeroFlag  = result == 0;
				core.SignFlag  = (result & 0x80) != 0;
				break;
			case 0x10: // ADC r/m8, r8
				{ uint c = core.CarryFlag ? 1u : 0u;
				  uint r = (uint)dst + src + c;
				  result = (byte)r;
				  core.CarryFlag    = r > 0xFF;
				  core.OverflowFlag = (~(dst ^ src) & (dst ^ result) & 0x80) != 0;
				  core.ZeroFlag     = result == 0;
				  core.SignFlag     = (result & 0x80) != 0; }
				break;
			case 0x18: // SBB r/m8, r8
				{ uint b = core.CarryFlag ? 1u : 0u;
				  uint r = (uint)dst - src - b;
				  result = (byte)r;
				  core.CarryFlag    = r > 0xFF;
				  core.OverflowFlag = ((dst ^ src) & (dst ^ result) & 0x80) != 0;
				  core.ZeroFlag     = result == 0;
				  core.SignFlag     = (result & 0x80) != 0; }
				break;
			case 0x20: // AND r/m8, r8
				result = (byte)(dst & src);
				core.CarryFlag = core.OverflowFlag = false;
				core.ZeroFlag  = result == 0;
				core.SignFlag  = (result & 0x80) != 0;
				break;
			case 0x28: // SUB r/m8, r8
				result = (byte)(dst - src);
				core.CarryFlag    = dst < src;
				core.OverflowFlag = ((dst ^ src) & (dst ^ result) & 0x80) != 0;
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				break;
			case 0x30: // XOR r/m8, r8
				result = (byte)(dst ^ src);
				core.CarryFlag = core.OverflowFlag = false;
				core.ZeroFlag  = result == 0;
				core.SignFlag  = (result & 0x80) != 0;
				break;
			case 0x38: // CMP r/m8, r8
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
		{
			if ( mod == 3 )
				SetReg8( core, rm, result );
			else
				core.WriteByte( destAddr, result );
		}

		core.Registers["eip"] += instrLen;
	}

	private byte GetReg8( X86Core core, byte regCode )
	{
		string regName = regCode switch
		{
			0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
			4 => "eax", 5 => "ecx", 6 => "edx", 7 => "ebx", _ => "eax"
		};
		return regCode < 4
			? (byte)(core.Registers[regName] & 0xFF)
			: (byte)((core.Registers[regName] >> 8) & 0xFF);
	}

	private void SetReg8( X86Core core, byte regCode, byte value )
	{
		string regName = regCode switch
		{
			0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
			4 => "eax", 5 => "ecx", 6 => "edx", 7 => "ebx", _ => "eax"
		};
		if ( regCode < 4 )
			core.Registers[regName] = (core.Registers[regName] & 0xFFFFFF00) | value;
		else
			core.Registers[regName] = (core.Registers[regName] & 0xFFFF00FF) | ((uint)value << 8);
	}
}
