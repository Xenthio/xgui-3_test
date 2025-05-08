namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class RetHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xC3 || opcode == 0xC2;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		// Track function exit for debugging - not real CPU behavior
		core.ExitFunction();

		// For RET imm16, read the immediate value BEFORE popping the return address
		ushort imm16 = 0;
		if ( opcode == 0xC2 ) // RET imm16
		{
			// Read 16-bit immediate value
			imm16 = (ushort)(core.ReadByte( eip + 1 ) | (core.ReadByte( eip + 2 ) << 8));
		}

		// Get return address from stack
		uint returnAddress = core.Pop();

		// For RET imm16, adjust stack after popping
		if ( opcode == 0xC2 )
		{
			// Adjust stack pointer by immediate value
			core.Registers["esp"] += imm16;
		}

		// Set EIP to return address
		core.Registers["eip"] = returnAddress;
	}
}
