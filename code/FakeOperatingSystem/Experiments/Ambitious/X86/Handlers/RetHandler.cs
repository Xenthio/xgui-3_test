namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class RetHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xC3 || opcode == 0xC2;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		// Track function exit for debugging - not real CPU behavior but useful
		core.ExitFunction();

		if ( opcode == 0xC3 ) // RET
		{
			// Simple RET - pop return address and jump
			core.Registers["eip"] = core.Pop();
		}
		else // RET imm16
		{
			ushort imm16 = (ushort)(core.ReadByte( eip + 1 ) | (core.ReadByte( eip + 2 ) << 8));

			// RET imm16 - pop return address, add imm16 to ESP, jump
			core.Registers["eip"] = core.Pop();
			core.Registers["esp"] += imm16;
		}

		// Keep program exit detection logic for emulator functionality
		// but separate it from CPU behavior emulation
		if ( core.Registers["eip"] < 0x1000 || core.Registers["eip"] > 0xF0000000 )
		{
			Log.Warning( $"RET jumped to invalid address 0x{core.Registers["eip"]:X8}, treating as program exit" );
			core.Registers["eip"] = 0xFFFFFFFF; // Special emulator value
		}
	}
}
