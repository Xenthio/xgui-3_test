namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class FlagInstructionHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0xF8 ||  // CLC - Clear Carry Flag
		opcode == 0xF9 ||  // STC - Set Carry Flag
		opcode == 0xFA ||  // CLI - Clear Interrupt Flag
		opcode == 0xFB ||  // STI - Set Interrupt Flag
		opcode == 0xFC ||  // CLD - Clear Direction Flag
		opcode == 0xFD;    // STD - Set Direction Flag

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		switch ( opcode )
		{
			case 0xF8: // CLC
				core.CarryFlag = false;
				break;

			case 0xF9: // STC
				core.CarryFlag = true;
				break;

			case 0xFA: // CLI
				core.InterruptFlag = false;
				break;

			case 0xFB: // STI
				core.InterruptFlag = true;
				break;

			case 0xFC: // CLD
				core.DirectionFlag = false;
				break;

			case 0xFD: // STD
				core.DirectionFlag = true;
				break;
		}

		// Advance EIP
		core.Registers["eip"] += 1;
	}
}
