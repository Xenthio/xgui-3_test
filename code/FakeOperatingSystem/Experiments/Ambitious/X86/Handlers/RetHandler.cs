namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class RetHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xC3 || opcode == 0xC2;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		uint espBefore = core.Registers["esp"];
		byte opcode = core.ReadByte( eip );

		// Track function exit for debugging - not real CPU behavior
		core.ExitFunction();

		ushort imm16 = 0;
		if ( opcode == 0xC2 ) // RET imm16
		{
			imm16 = (ushort)(core.ReadByte( eip + 1 ) | (core.ReadByte( eip + 2 ) << 8));
			Log.Info( $"RET imm16: EIP=0x{eip:X8}, ESP(before)=0x{espBefore:X8}, imm16=0x{imm16:X4}" );
		}
		else
		{
			Log.Info( $"RET: EIP=0x{eip:X8}, ESP(before)=0x{espBefore:X8}" );
		}

		// Get return address from stack
		uint returnAddress = core.Pop();
		uint espAfterPop = core.Registers["esp"];
		Log.Info( $"RET: Popped return address=0x{returnAddress:X8}, ESP(after pop)=0x{espAfterPop:X8}" );

		if ( opcode == 0xC2 )
		{
			core.Registers["esp"] += imm16;
			Log.Info( $"RET imm16: ESP(after add)=0x{core.Registers["esp"]:X8}" );
		}

		core.Registers["eip"] = returnAddress;
		Log.Info( $"RET: EIP set to 0x{returnAddress:X8}, ESP(final)=0x{core.Registers["esp"]:X8}" );
	}
}
