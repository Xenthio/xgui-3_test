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

		// Get return address from stack
		uint returnAddress = core.Pop();

		if ( opcode == 0xC2 ) // RET imm16
		{
			// Read 16-bit immediate value
			ushort imm16 = (ushort)(core.ReadByte( eip + 1 ) | (core.ReadByte( eip + 2 ) << 8));

			// Adjust stack pointer after popping return address
			core.Registers["esp"] += imm16;
		}

		// Set EIP to return address
		core.Registers["eip"] = returnAddress;
	}
}
