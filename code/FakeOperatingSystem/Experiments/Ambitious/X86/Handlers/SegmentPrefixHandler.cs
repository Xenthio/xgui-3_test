namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class SegmentPrefixHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x64 || opcode == 0x65 || opcode == 0x2E || opcode == 0x3E;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		byte nextOpcode = core.ReadByte( eip + 1 );

		string segment = opcode switch
		{
			0x64 => "FS",
			0x65 => "GS",
			0x2E => "CS",
			0x3E => "DS",
			_ => "??"
		};

		// Most common pattern is FS:[0] access (0x64 0xA1 0x00 0x00 0x00 0x00)
		// which is often used to get the TEB pointer
		if ( opcode == 0x64 && nextOpcode == 0xA1 )
		{
			uint offset = core.ReadDword( eip + 2 );
			if ( offset == 0 )
			{
				// Special case for FS:[0] - return a dummy TEB pointer
				core.Registers["eax"] = 0x00100000; // Dummy TEB address
				core.Registers["eip"] += 6; // Skip the whole instruction
				Log.Info( $"Emulated FS:[0] access - provided dummy TEB pointer 0x{core.Registers["eax"]:X8}" );
				return;
			}
		}

		Log.Warning( $"[STUB!] Segment prefix {segment}: ignored (treating as flat memory model)" );

		// Skip the prefix byte
		core.Registers["eip"] += 1;
	}
}
