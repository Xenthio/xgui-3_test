using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// String operations: REP/REPNE prefixes + MOVS/STOS/LODS/SCAS/CMPS variants.
/// Fixed: REP state is now instance-level (not static), full STOSD/LODSD/STOSW/CMPSB support,
/// REP SCAS terminates correctly, ECX decrements inside the prefix handler.
/// </summary>
public class StringOperationsHandler : IInstructionHandler
{
	// Instance-level REP state — not static, so independent X86Core instances don't share it
	private bool _hasRepPrefix = false;
	private bool _isRepne = false; // REPNE/REPNZ vs REP/REPE/REPZ
	private bool _rep16bit = false; // true when 0x66 prefix follows REP (STOSW, MOVSW)

	public bool CanHandle( byte opcode ) =>
		opcode == 0xF2 || // REPNE/REPNZ prefix
		opcode == 0xF3 || // REP/REPE/REPZ prefix
		opcode == 0xA4 || // MOVSB
		opcode == 0xA5 || // MOVSD
		opcode == 0xA6 || // CMPSB
		opcode == 0xA7 || // CMPSD
		opcode == 0xAA || // STOSB
		opcode == 0xAB || // STOSD
		opcode == 0xAC || // LODSB
		opcode == 0xAD || // LODSD
		opcode == 0xAE || // SCASB
		opcode == 0xAF;   // SCASD

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		// ── REP prefix — record and advance ──────────────────────────────────
		if ( opcode == 0xF2 || opcode == 0xF3 )
		{
			_hasRepPrefix = true;
			_isRepne = (opcode == 0xF2);
			core.Registers["eip"]++;
			// Peek at the next byte — if it's 0x66 (operand-size prefix) consume it too
			// and mark that we want the 16-bit (W) variants of string ops
			uint nextEip = core.Registers["eip"];
			byte nextByte = core.ReadByte( nextEip );
			if ( nextByte == 0x66 )
			{
				_rep16bit = true;
				core.Registers["eip"]++; // consume the 0x66 prefix
			}
			else
			{
				_rep16bit = false;
			}
			return;
		}

		// ── Execute the string operation (possibly under REP) ─────────────────
		switch ( opcode )
		{
			case 0xA4: ExecuteRep( core, opcode, false, ExecuteMovsb ); break; // MOVSB
			case 0xA5: ExecuteRep( core, opcode, false, _rep16bit ? ExecuteMovsw : ExecuteMovsd ); break; // MOVSD or MOVSW
			case 0xA6: ExecuteRepCmps( core, false ); break;                   // CMPSB
			case 0xA7: ExecuteRepCmps( core, true ); break;                    // CMPSD
			case 0xAA: ExecuteRep( core, opcode, false, ExecuteStosb ); break; // STOSB
			case 0xAB: ExecuteRep( core, opcode, false, _rep16bit ? ExecuteStosw : ExecuteStosd ); break; // STOSD or STOSW
			case 0xAC: ExecuteLodsb( core ); break;                            // LODSB (REP makes no sense for LODS)
			case 0xAD: ExecuteLodsd( core ); break;                            // LODSD
			case 0xAE: ExecuteRepScas( core, false ); break;                   // SCASB
			case 0xAF: ExecuteRepScas( core, true ); break;                    // SCASD
			default:
				Log.Warning( $"StringOperationsHandler: unhandled opcode 0x{opcode:X2}" );
				break;
		}

		core.Registers["eip"]++;
		_hasRepPrefix = false;
		_isRepne = false;
	}

	// ── Generic REP loop for MOVS / STOS (no flag termination) ───────────────

	private void ExecuteRep( X86Core core, byte opcode, bool wide, Action<X86Core> oneStep )
	{
		if ( _hasRepPrefix )
		{
			while ( core.Registers["ecx"] != 0 )
			{
				oneStep( core );
				core.Registers["ecx"]--;
			}
		}
		else
		{
			oneStep( core );
		}
	}

	// ── MOVSB ────────────────────────────────────────────────────────────────

	private static void ExecuteMovsb( X86Core core )
	{
		uint esi = core.Registers["esi"];
		uint edi = core.Registers["edi"];
		core.WriteByte( edi, core.ReadByte( esi ) );
		int delta = core.DirectionFlag ? -1 : 1;
		core.Registers["esi"] = (uint)(esi + delta);
		core.Registers["edi"] = (uint)(edi + delta);
	}

	// ── MOVSD ────────────────────────────────────────────────────────────────

	private static void ExecuteMovsd( X86Core core )
	{
		uint esi = core.Registers["esi"];
		uint edi = core.Registers["edi"];
		core.WriteDword( edi, core.ReadDword( esi ) );
		int delta = core.DirectionFlag ? -4 : 4;
		core.Registers["esi"] = (uint)(esi + delta);
		core.Registers["edi"] = (uint)(edi + delta);
	}

	// ── STOSB ────────────────────────────────────────────────────────────────

	private static void ExecuteStosb( X86Core core )
	{
		uint edi = core.Registers["edi"];
		core.WriteByte( edi, (byte)(core.Registers["eax"] & 0xFF) );
		core.Registers["edi"] = (uint)(edi + (core.DirectionFlag ? -1 : 1));
	}

	// ── STOSD ────────────────────────────────────────────────────────────────

	private static void ExecuteStosd( X86Core core )
	{
		uint edi = core.Registers["edi"];
		core.WriteDword( edi, core.Registers["eax"] );
		core.Registers["edi"] = (uint)(edi + (core.DirectionFlag ? -4 : 4));
	}

	private static void ExecuteStosw( X86Core core )
	{
		uint edi = core.Registers["edi"];
		core.WriteWord( edi, (ushort)(core.Registers["eax"] & 0xFFFF) );
		core.Registers["edi"] = (uint)(edi + (core.DirectionFlag ? -2 : 2));
	}

	private static void ExecuteMovsw( X86Core core )
	{
		uint esi = core.Registers["esi"];
		uint edi = core.Registers["edi"];
		core.WriteWord( edi, (ushort)(core.ReadByte( esi ) | (core.ReadByte( esi + 1 ) << 8)) );
		int delta = core.DirectionFlag ? -2 : 2;
		core.Registers["esi"] = (uint)(esi + delta);
		core.Registers["edi"] = (uint)(edi + delta);
	}

	// ── LODSB ────────────────────────────────────────────────────────────────

	private static void ExecuteLodsb( X86Core core )
	{
		uint esi = core.Registers["esi"];
		byte val = core.ReadByte( esi );
		core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | val;
		core.Registers["esi"] = (uint)(esi + (core.DirectionFlag ? -1 : 1));
		core.LogVerbose( $"LODSB: AL=0x{val:X2} from [0x{esi:X8}]" );
	}

	// ── LODSD ────────────────────────────────────────────────────────────────

	private static void ExecuteLodsd( X86Core core )
	{
		uint esi = core.Registers["esi"];
		core.Registers["eax"] = core.ReadDword( esi );
		core.Registers["esi"] = (uint)(esi + (core.DirectionFlag ? -4 : 4));
	}

	// ── REP SCAS (SCASB / SCASD) ─────────────────────────────────────────────

	private void ExecuteRepScas( X86Core core, bool wide )
	{
		if ( _hasRepPrefix )
		{
			while ( core.Registers["ecx"] != 0 )
			{
				ScasStep( core, wide );
				core.Registers["ecx"]--;
				// REPE (F3): stop if ZF cleared (values differ)
				// REPNE (F2): stop if ZF set (values equal)
				bool stopCond = _isRepne ? core.ZeroFlag : !core.ZeroFlag;
				if ( stopCond ) break;
			}
		}
		else
		{
			ScasStep( core, wide );
		}
	}

	private static void ScasStep( X86Core core, bool wide )
	{
		uint edi = core.Registers["edi"];
		if ( wide )
		{
			uint eax = core.Registers["eax"];
			uint mem = core.ReadDword( edi );
			uint r = eax - mem;
			SetFlagsSub32( core, eax, mem, r );
			core.Registers["edi"] = (uint)(edi + (core.DirectionFlag ? -4 : 4));
		}
		else
		{
			byte al = (byte)(core.Registers["eax"] & 0xFF);
			byte mem = core.ReadByte( edi );
			byte r = (byte)(al - mem);
			core.ZeroFlag = r == 0;
			core.SignFlag = (r & 0x80) != 0;
			core.CarryFlag = al < mem;
			core.OverflowFlag = ((al ^ mem) & (al ^ r) & 0x80) != 0;
			core.Registers["edi"] = (uint)(edi + (core.DirectionFlag ? -1 : 1));
		}
	}

	// ── REP CMPS ─────────────────────────────────────────────────────────────

	private void ExecuteRepCmps( X86Core core, bool wide )
	{
		if ( _hasRepPrefix )
		{
			while ( core.Registers["ecx"] != 0 )
			{
				CmpsStep( core, wide );
				core.Registers["ecx"]--;
				bool stopCond = _isRepne ? core.ZeroFlag : !core.ZeroFlag;
				if ( stopCond ) break;
			}
		}
		else
		{
			CmpsStep( core, wide );
		}
	}

	private static void CmpsStep( X86Core core, bool wide )
	{
		uint esi = core.Registers["esi"];
		uint edi = core.Registers["edi"];
		if ( wide )
		{
			uint sv = core.ReadDword( esi );
			uint dv = core.ReadDword( edi );
			uint r = sv - dv;
			SetFlagsSub32( core, sv, dv, r );
			int delta = core.DirectionFlag ? -4 : 4;
			core.Registers["esi"] = (uint)(esi + delta);
			core.Registers["edi"] = (uint)(edi + delta);
		}
		else
		{
			byte sv = core.ReadByte( esi );
			byte dv = core.ReadByte( edi );
			byte r = (byte)(sv - dv);
			core.ZeroFlag = r == 0;
			core.SignFlag = (r & 0x80) != 0;
			core.CarryFlag = sv < dv;
			core.OverflowFlag = ((sv ^ dv) & (sv ^ r) & 0x80) != 0;
			int delta = core.DirectionFlag ? -1 : 1;
			core.Registers["esi"] = (uint)(esi + delta);
			core.Registers["edi"] = (uint)(edi + delta);
		}
	}

	// ── Flag helper ───────────────────────────────────────────────────────────

	private static void SetFlagsSub32( X86Core core, uint dst, uint src, uint result )
	{
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = dst < src;
		bool ds = (dst & 0x80000000) != 0;
		bool ss = (src & 0x80000000) != 0;
		bool rs = (result & 0x80000000) != 0;
		core.OverflowFlag = (ds != ss) && (rs != ds);
	}
}
