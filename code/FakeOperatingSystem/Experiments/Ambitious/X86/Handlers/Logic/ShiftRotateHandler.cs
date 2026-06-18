using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// Handles shift/rotate group opcodes: C0/C1 (imm8), D0/D1 (by 1), D2/D3 (by CL).
/// C0/D0/D2 operate on r/m8; C1/D1/D3 on r/m32.
/// Fixed: proper 8-bit vs 32-bit separation, count mask (& 0x1F for 32-bit, & 0x1F for 8-bit
/// masked to [0,7] where needed), correct flags, no code duplication.
/// </summary>
public class ShiftRotateHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0xC0 || opcode == 0xC1 ||
		opcode == 0xD0 || opcode == 0xD1 ||
		opcode == 0xD2 || opcode == 0xD3;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte regOp = (byte)((modrm >> 3) & 0x7); // shift operation
		byte rm = (byte)(modrm & 0x7);

		bool is8Bit = (opcode == 0xC0 || opcode == 0xD0 || opcode == 0xD2);

		// ── Decode shift count ────────────────────────────────────────────────
		int count;
		uint instrLen; // total bytes for this instruction

		switch ( opcode )
		{
			case 0xC0:
			case 0xC1:
				// imm8 follows modrm (and SIB/disp if mem)
				if ( mod == 3 )
				{
					count = core.ReadByte( eip + 2 ) & 0x1F;
					instrLen = 3;
				}
				else
				{
					uint modrmLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip ) - 1;
					count = core.ReadByte( (uint)(eip + 1 + modrmLen) ) & 0x1F;
					instrLen = 1 + modrmLen + 1; // opcode + modrm/SIB/disp + imm8
				}
				break;
			case 0xD0:
			case 0xD1:
				count = 1;
				instrLen = (mod == 3)
					? 2u
					: (uint)(X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
				break;
			default: // D2/D3 — shift by CL
				count = (int)(core.Registers["ecx"] & 0x1F);
				instrLen = (mod == 3)
					? 2u
					: (uint)(X86AddressingHelper.GetInstructionLength( modrm, core, eip ));
				break;
		}

		// ── Fetch value ───────────────────────────────────────────────────────
		uint addr = 0;
		uint value32;
		byte value8;

		if ( mod == 3 )
		{
			string rname = X86AddressingHelper.GetRegisterName( rm );
			value32 = core.Registers[rname];
			value8 = (byte)(value32 & 0xFF);
		}
		else
		{
			addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			if ( is8Bit )
			{
				value8 = core.ReadByte( addr );
				value32 = value8;
			}
			else
			{
				value32 = core.ReadDword( addr );
				value8 = (byte)(value32 & 0xFF);
			}
		}

		// ── Execute ───────────────────────────────────────────────────────────
		uint result32;
		byte result8;

		if ( is8Bit )
		{
			result8 = Shift8( core, regOp, value8, count );
			result32 = result8;
		}
		else
		{
			result32 = Shift32( core, regOp, value32, count );
			result8 = 0; // unused
		}

		// ── Write back ────────────────────────────────────────────────────────
		if ( mod == 3 )
		{
			string rname = X86AddressingHelper.GetRegisterName( rm );
			if ( is8Bit )
				core.Registers[rname] = (core.Registers[rname] & 0xFFFFFF00) | result8;
			else
				core.Registers[rname] = result32;
		}
		else
		{
			if ( is8Bit )
				core.WriteByte( addr, result8 );
			else
				core.WriteDword( addr, result32 );
		}

		core.Registers["eip"] += instrLen;
	}

	// ── 32-bit shift/rotate ───────────────────────────────────────────────────

	private static uint Shift32( X86Core core, byte op, uint value, int count )
	{
		uint result = value;
		if ( count == 0 ) return value; // count=0: no flags changed per Intel spec

		switch ( op )
		{
			case 0: // ROL
			{
				int c = count & 31;
				result = (c == 0) ? value : (value << c) | (value >> (32 - c));
				core.CarryFlag = (result & 1) != 0;
				if ( count == 1 )
					core.OverflowFlag = ((result >> 31) ^ (result & 1)) != 0;
				break;
			}
			case 1: // ROR
			{
				int c = count & 31;
				result = (c == 0) ? value : (value >> c) | (value << (32 - c));
				core.CarryFlag = (result >> 31) != 0;
				if ( count == 1 )
					core.OverflowFlag = (((result >> 31) & 1) ^ ((result >> 30) & 1)) != 0;
				break;
			}
			case 2: // RCL — rotate through carry
			{
				for ( int i = 0; i < count; i++ )
				{
					uint cin = core.CarryFlag ? 1u : 0u;
					core.CarryFlag = (value & 0x80000000) != 0;
					value = (value << 1) | cin;
				}
				result = value;
				if ( count == 1 )
					core.OverflowFlag = ((result >> 31 & 1) ^ (core.CarryFlag ? 1u : 0u)) != 0;
				break;
			}
			case 3: // RCR — rotate through carry
			{
				for ( int i = 0; i < count; i++ )
				{
					uint cin = core.CarryFlag ? 0x80000000u : 0u;
					core.CarryFlag = (value & 1) != 0;
					value = (value >> 1) | cin;
				}
				result = value;
				if ( count == 1 )
					core.OverflowFlag = (((result >> 31) & 1) ^ ((result >> 30) & 1)) != 0;
				break;
			}
			case 4: // SHL / SAL
			case 6: // SAL alias
			{
				core.CarryFlag = count <= 32 && ((value >> (32 - count)) & 1) != 0;
				result = (count >= 32) ? 0 : (value << count);
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80000000) != 0;
				core.OverflowFlag = count == 1 && (core.SignFlag ^ core.CarryFlag);
				break;
			}
			case 5: // SHR
			{
				core.CarryFlag = count <= 32 && ((value >> (count - 1)) & 1) != 0;
				result = (count >= 32) ? 0 : (value >> count);
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80000000) != 0;
				if ( count == 1 ) core.OverflowFlag = (value & 0x80000000) != 0; // MSB of original
				break;
			}
			case 7: // SAR
			{
				int signed = (int)value;
				core.CarryFlag = count <= 32 && (((uint)signed >> (count - 1)) & 1) != 0;
				result = (uint)((count >= 32) ? (signed >> 31) : (signed >> count));
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80000000) != 0;
				if ( count == 1 ) core.OverflowFlag = false; // SAR never sets OF
				break;
			}
			default:
				Log.Warning( $"ShiftRotate: unimplemented 32-bit op {op}" );
				break;
		}
		return result;
	}

	// ── 8-bit shift/rotate ────────────────────────────────────────────────────

	private static byte Shift8( X86Core core, byte op, byte value, int count )
	{
		byte result = value;
		if ( count == 0 ) return value;

		switch ( op )
		{
			case 0: // ROL
			{
				int c = count & 7;
				result = (c == 0) ? value : (byte)((value << c) | (value >> (8 - c)));
				core.CarryFlag = (result & 1) != 0;
				if ( count == 1 ) core.OverflowFlag = ((result >> 7) ^ (result & 1)) != 0;
				break;
			}
			case 1: // ROR
			{
				int c = count & 7;
				result = (c == 0) ? value : (byte)((value >> c) | (value << (8 - c)));
				core.CarryFlag = (result >> 7) != 0;
				if ( count == 1 ) core.OverflowFlag = (((result >> 7) & 1) ^ ((result >> 6) & 1)) != 0;
				break;
			}
			case 2: // RCL
			{
				for ( int i = 0; i < count; i++ )
				{
					byte cin = (byte)(core.CarryFlag ? 1 : 0);
					core.CarryFlag = (value & 0x80) != 0;
					value = (byte)((value << 1) | cin);
				}
				result = value;
				break;
			}
			case 3: // RCR
			{
				for ( int i = 0; i < count; i++ )
				{
					byte cin = (byte)(core.CarryFlag ? 0x80 : 0);
					core.CarryFlag = (value & 1) != 0;
					value = (byte)((value >> 1) | cin);
				}
				result = value;
				break;
			}
			case 4: // SHL
			case 6: // SAL (identical to SHL)
			{
				core.CarryFlag = count <= 8 && ((value >> (8 - count)) & 1) != 0;
				result = (byte)(count >= 8 ? 0 : (value << count));
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80) != 0;
				core.OverflowFlag = count == 1 && (core.SignFlag ^ core.CarryFlag);
				break;
			}
			case 5: // SHR
			{
				core.CarryFlag = count <= 8 && ((value >> (count - 1)) & 1) != 0;
				result = (byte)(count >= 8 ? 0 : (value >> count));
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80) != 0;
				if ( count == 1 ) core.OverflowFlag = (value & 0x80) != 0;
				break;
			}
			case 7: // SAR
			{
				sbyte sv = (sbyte)value;
				core.CarryFlag = count <= 8 && (((byte)(uint)sv >> (count - 1)) & 1) != 0;
				result = (byte)(count >= 8 ? (sv >> 7) : (sv >> count));
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80) != 0;
				if ( count == 1 ) core.OverflowFlag = false;
				break;
			}
			default:
				Log.Warning( $"ShiftRotate: unimplemented 8-bit op {op}" );
				break;
		}
		return result;
	}


}
