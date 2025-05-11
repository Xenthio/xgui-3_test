using FakeDesktop;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class RetHandler : IInstructionHandler
{
	private readonly X86Interpreter _interpreter;
	public RetHandler( X86Interpreter interpreter )
	{
		_interpreter = interpreter;
	}
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
			core.LogVerbose( $"RET imm16: EIP=0x{eip:X8}, ESP(before)=0x{espBefore:X8}, imm16=0x{imm16:X4}" );
		}
		else
		{
			core.LogVerbose( $"RET: EIP=0x{eip:X8}, ESP(before)=0x{espBefore:X8}" );
		}

		// Get return address from stack
		uint returnAddress = core.Pop();
		uint espAfterPop = core.Registers["esp"];
		core.LogVerbose( $"RET: Popped return address=0x{returnAddress:X8}, ESP(after pop)=0x{espAfterPop:X8}" );

		if ( returnAddress == 0xFFFFFFFF )
		{
			core.Registers["eip"] = 0xFFFFFFFF;
			core.LogVerbose( $"RET: Program exit sentinel reached (0x{returnAddress:X8})." );
			return;
		}
		if ( returnAddress == 0x00030000 )
		{
			core.Registers["eip"] = 0xFFFFFFFF;
			Log.Warning( $"RET: Non-standard sentinel address reached (0x{returnAddress:X8})." );
			return;
		}
		// truly invalid addresses (not code, not sentinel)
		if ( returnAddress < 0x00400000 )
		{
			core.Registers["eip"] = 0xFFFFFFFF;

			_interpreter.HaltWithMessageBox( "Invalid Return Address",
				$"Invalid return address: 0x{returnAddress:X8}\n" +
				$"This could indicate a stack corruption or an invalid return address.\n" +
				$"But could also be just normal program exit.\n" +
				$"If so, it is safe to ignore this message.",
				MessageBoxIcon.Information,
				MessageBoxButtons.OK
			);

			Log.Warning( $"RET: Return to invalid address 0x{returnAddress:X8}, treating as program exit." );
			return;
		}

		if ( opcode == 0xC2 )
		{
			core.Registers["esp"] += imm16;
			core.LogVerbose( $"RET imm16: ESP(after add)=0x{core.Registers["esp"]:X8}" );
		}

		core.Registers["eip"] = returnAddress;
		core.LogVerbose( $"RET: EIP set to 0x{returnAddress:X8}, ESP(final)=0x{core.Registers["esp"]:X8}" );
	}
}
