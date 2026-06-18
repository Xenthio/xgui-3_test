namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class PortIOHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0xE4 || // IN AL, imm8
		opcode == 0xE5 || // IN EAX, imm8
		opcode == 0xE6 || // OUT imm8, AL
		opcode == 0xE7 || // OUT imm8, EAX
		opcode == 0xEC || // IN AL, DX
		opcode == 0xED || // IN EAX, DX
		opcode == 0xEE || // OUT DX, AL
		opcode == 0xEF || // OUT DX, EAX
		opcode == 0x6C || // INSB  — input string byte from DX to [EDI]
		opcode == 0x6D || // INSD  — input string dword from DX to [EDI]
		opcode == 0x6E || // OUTSB — output string byte from [ESI] to DX
		opcode == 0x6F;   // OUTSD — output string dword from [ESI] to DX

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		switch ( opcode )
		{
			case 0xE4: // IN AL, imm8 — 2 bytes
				{
					byte port = core.ReadByte( eip + 1 );
					core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | VirtualPortIO( core, port, 1 );
					core.LogVerbose( $"IN AL, 0x{port:X2}" );
					core.Registers["eip"] += 2;
				}
				break;

			case 0xE5: // IN EAX, imm8 — 2 bytes
				{
					byte port = core.ReadByte( eip + 1 );
					core.Registers["eax"] = VirtualPortIO( core, port, 4 );
					core.LogVerbose( $"IN EAX, 0x{port:X2}" );
					core.Registers["eip"] += 2;
				}
				break;

			case 0xE6: // OUT imm8, AL — 2 bytes
				{
					byte port = core.ReadByte( eip + 1 );
					core.LogVerbose( $"OUT 0x{port:X2}, AL=0x{(core.Registers["eax"]&0xFF):X2}" );
					core.Registers["eip"] += 2;
				}
				break;

			case 0xE7: // OUT imm8, EAX — 2 bytes
				{
					byte port = core.ReadByte( eip + 1 );
					core.LogVerbose( $"OUT 0x{port:X2}, EAX=0x{core.Registers["eax"]:X8}" );
					core.Registers["eip"] += 2;
				}
				break;

			case 0xEC: // IN AL, DX — 1 byte
				{
					ushort port = (ushort)core.Registers["edx"];
					core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | VirtualPortIO( core, port, 1 );
					core.LogVerbose( $"IN AL, DX (port=0x{port:X4})" );
					core.Registers["eip"] += 1;
				}
				break;

			case 0xED: // IN EAX, DX — 1 byte
				{
					ushort port = (ushort)core.Registers["edx"];
					core.Registers["eax"] = VirtualPortIO( core, port, 4 );
					core.LogVerbose( $"IN EAX, DX (port=0x{port:X4})" );
					core.Registers["eip"] += 1;
				}
				break;

			case 0xEE: // OUT DX, AL — 1 byte
				{
					ushort port = (ushort)core.Registers["edx"];
					core.LogVerbose( $"OUT DX (0x{port:X4}), AL=0x{(core.Registers["eax"]&0xFF):X2}" );
					core.Registers["eip"] += 1;
				}
				break;

			case 0xEF: // OUT DX, EAX — 1 byte
				{
					ushort port = (ushort)core.Registers["edx"];
					core.LogVerbose( $"OUT DX (0x{port:X4}), EAX=0x{core.Registers["eax"]:X8}" );
					core.Registers["eip"] += 1;
				}
				break;

			case 0x6C: // INSB — read byte from port DX into [EDI], advance EDI
				{
					ushort port = (ushort)core.Registers["edx"];
					core.WriteByte( core.Registers["edi"], VirtualPortIO( core, port, 1 ) );
					core.Registers["edi"] += (uint)(core.DirectionFlag ? -1 : 1);
					core.Registers["eip"] += 1;
				}
				break;

			case 0x6D: // INSD — read dword from port DX into [EDI], advance EDI
				{
					ushort port = (ushort)core.Registers["edx"];
					core.WriteDword( core.Registers["edi"], VirtualPortIO( core, port, 4 ) );
					core.Registers["edi"] += (uint)(core.DirectionFlag ? -4 : 4);
					core.Registers["eip"] += 1;
				}
				break;

			case 0x6E: // OUTSB — output byte from [ESI] to port DX, advance ESI
				{
					ushort port = (ushort)core.Registers["edx"];
					core.LogVerbose( $"OUTSB port=0x{port:X4} [ESI]=0x{core.Registers["esi"]:X8}" );
					core.Registers["esi"] += (uint)(core.DirectionFlag ? -1 : 1);
					core.Registers["eip"] += 1;
				}
				break;

			case 0x6F: // OUTSD — output dword from [ESI] to port DX, advance ESI
				{
					ushort port = (ushort)core.Registers["edx"];
					core.LogVerbose( $"OUTSD port=0x{port:X4} [ESI]=0x{core.Registers["esi"]:X8}" );
					core.Registers["esi"] += (uint)(core.DirectionFlag ? -4 : 4);
					core.Registers["eip"] += 1;
				}
				break;

			default:
				Log.Warning( $"Unimplemented port I/O opcode: 0x{opcode:X2} at EIP=0x{eip:X8}" );
				core.Registers["eip"] += 1;
				break;
		}
	}

	// Simulated port I/O
	private byte VirtualPortIO( X86Core core, ushort port, byte size )
	{
		// In a real emulator, this would interface with virtual hardware
		// For now, just return 0 for all ports
		Log.Warning( $"(EIP: 0x{core.Registers["eip"]:X8}) Virtual port I/O - Reading from port 0x{port:X4} (stubbed)" );
		return 0;
	}
}
