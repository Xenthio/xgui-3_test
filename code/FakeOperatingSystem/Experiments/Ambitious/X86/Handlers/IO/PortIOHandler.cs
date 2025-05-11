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
		opcode == 0xEF;   // OUT DX, EAX

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		switch ( opcode )
		{
			case 0xEC: // IN AL, DX
				{
					// Get port number from DX
					ushort port = (ushort)core.Registers["edx"];

					// For IN AL, DX - read a byte from the port into AL
					// In a virtual environment, we typically return 0 or simulate the hardware
					byte value = VirtualPortIO( core, port, 1 );

					// Store in AL (low byte of EAX), preserving other bytes
					core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | value;

					core.LogVerbose( $"IN AL, DX - Read 0x{value:X2} from port 0x{port:X4}" );
					core.Registers["eip"] += 1;
				}
				break;

			// Implement other port I/O operations similarly
			// For now just advance EIP and stub them out
			default:
				Log.Warning( $"Unimplemented port I/O instruction: 0x{opcode:X2}" );
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
