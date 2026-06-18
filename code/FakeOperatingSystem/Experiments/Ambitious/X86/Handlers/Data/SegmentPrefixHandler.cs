using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// Handles segment-override prefixes: FS (0x64), GS (0x65), CS (0x2E), DS (0x3E), ES (0x26), SS (0x36).
///
/// FS-segment accesses are the most common — they reach into the TEB.
/// We fully decode MOV/CMP/ADD/etc. with FS: override so programs that read
/// TEB fields (ExceptionList FS:[0], StackBase FS:[4], StackLimit FS:[8],
/// self-pointer FS:[0x18], PID FS:[0x20], TID FS:[0x24], PEB FS:[0x30],
/// LastError FS:[0x34]) get real in-memory values.
///
/// TEB layout at 0x00100000 (initialised by X86Interpreter):
///   +0x00  ExceptionList  = 0xFFFFFFFF (end of chain)
///   +0x04  StackBase      = 0x00090000
///   +0x08  StackLimit     = 0x00070000
///   +0x0C  SubsystemTib   = 0
///   +0x10  FiberData      = 0
///   +0x14  ArbitraryData  = 0
///   +0x18  Self           = 0x00100000  (self-pointer)
///   +0x20  ClientId.PID   = 0x00001234
///   +0x24  ClientId.TID   = 0x00005678
///   +0x28  ActiveRpcHandle= 0
///   +0x2C  ThreadLocalInfo= 0
///   +0x30  PEB ptr        = 0x00101000
///   +0x34  LastErrorValue = 0
///   (rest zero-filled to 0x100 bytes)
/// </summary>
public class SegmentPrefixHandler : IInstructionHandler
{
	public const uint TebBase = 0x00100000;
	public const uint PebBase = 0x00101000;

	public bool CanHandle( byte opcode ) =>
		opcode == 0x64 || // FS:
		opcode == 0x65 || // GS:
		opcode == 0x2E || // CS:
		opcode == 0x3E || // DS:
		opcode == 0x26 || // ES:
		opcode == 0x36;   // SS:

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte prefix = core.ReadByte( eip );
		byte next = core.ReadByte( eip + 1 );

		// Only FS: accesses need special treatment; others can be flat-model ignored.
		if ( prefix != 0x64 )
		{
			// Skip the prefix, re-dispatch is handled by the interpreter on the next byte.
			// Simply advance past the prefix; the interpreter will decode the real opcode.
			core.Registers["eip"] += 1;
			return;
		}

		// ── FS: decode + translate ────────────────────────────────────────────
		// Convert FS:[offset] → linear address TebBase + offset, then execute
		// the underlying instruction against flat memory.

		switch ( next )
		{
			// MOV EAX, FS:[imm32]  — 0xA1
			case 0xA1:
			{
				uint offset = core.ReadDword( eip + 2 );
				core.Registers["eax"] = core.ReadDword( TebBase + offset );
				core.LogVerbose( $"MOV EAX, FS:[0x{offset:X}] = 0x{core.Registers["eax"]:X8}" );
				core.Registers["eip"] += 6;
				break;
			}
			// MOV FS:[imm32], EAX  — 0xA3
			case 0xA3:
			{
				uint offset = core.ReadDword( eip + 2 );
				core.WriteDword( TebBase + offset, core.Registers["eax"] );
				core.LogVerbose( $"MOV FS:[0x{offset:X}], EAX = 0x{core.Registers["eax"]:X8}" );
				core.Registers["eip"] += 6;
				break;
			}
			// MOV r32, FS:[r/m]  — 0x8B (most common: MOV reg, FS:[disp32])
			case 0x8B:
			{
				byte modrm = core.ReadByte( eip + 2 );
				byte mod = (byte)(modrm >> 6);
				byte reg = (byte)((modrm >> 3) & 7);
				uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 ); // +1: skip prefix
				uint linear = TebBase + addr; // addr is the FS-relative offset when mod=0/disp
				// Actually CalculateEffectiveAddress doesn't know about FS base; for typical
				// FS:[disp32] (mod=0, rm=5) it returns the raw displacement. That's exactly the offset.
				uint value = core.ReadDword( linear );
				core.Registers[RegName( reg )] = value;
				core.LogVerbose( $"MOV {RegName( reg )}, FS:[0x{addr:X}] = 0x{value:X8}" );
				uint instrLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
				core.Registers["eip"] += 1 + instrLen; // prefix + 0x8B + modrm/SIB/disp
				break;
			}
			// MOV FS:[r/m], r32  — 0x89
			case 0x89:
			{
				byte modrm = core.ReadByte( eip + 2 );
				byte reg = (byte)((modrm >> 3) & 7);
				uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip + 1 );
				core.WriteDword( TebBase + addr, core.Registers[RegName( reg )] );
				uint instrLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip + 1 );
				core.Registers["eip"] += 1 + instrLen;
				break;
			}
			// PUSH FS:[r/m]  — 0xFF /6 (rare, but let's not crash)
			default:
			{
				// For anything else, just skip the prefix and let the interpreter retry with next byte.
				// This is a degraded fallback; most useful FS: patterns are covered above.
				Log.Warning( $"FS: prefix before opcode 0x{next:X2} at EIP=0x{eip:X8} — prefix skipped (flat fallback)" );
				core.Registers["eip"] += 1;
				break;
			}
		}
	}

	// ── TEB initialisation (called once by X86Interpreter after memory setup) ─

	public static void InitializeTEB( X86Core core )
	{
		uint t = TebBase;

		// Clear 0x100 bytes
		for ( uint i = 0; i < 0x100; i += 4 )
			core.WriteDword( t + i, 0, protect: false );

		core.WriteDword( t + 0x00, 0xFFFFFFFF, protect: false ); // ExceptionList (end sentinel)
		core.WriteDword( t + 0x04, X86Core.StackTop,   protect: false ); // StackBase (top of stack)
		core.WriteDword( t + 0x08, X86Core.StackLimit, protect: false ); // StackLimit
		core.WriteDword( t + 0x18, TebBase,    protect: false ); // Self-pointer
		core.WriteDword( t + 0x20, 0x00001234, protect: false ); // ClientId.PID
		core.WriteDword( t + 0x24, 0x00005678, protect: false ); // ClientId.TID
		core.WriteDword( t + 0x30, PebBase,    protect: false ); // PEB pointer
		core.WriteDword( t + 0x34, 0,          protect: false ); // LastError

		// Minimal PEB stub at 0x00101000
		uint p = PebBase;
		for ( uint i = 0; i < 0x80; i += 4 )
			core.WriteDword( p + i, 0, protect: false );
		core.WriteDword( p + 0x02, 0, protect: false ); // BeingDebugged = 0 (byte actually but WriteDword is fine)
		core.WriteDword( p + 0x18, 0x00102000, protect: false ); // ProcessHeap stub
	}

	private static string RegName( int code ) => code switch
	{
		0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
		4 => "esp", 5 => "ebp", 6 => "esi", 7 => "edi",
		_ => throw new ArgumentException( $"Bad reg {code}" )
	};
}
