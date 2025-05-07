namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class FlagInstructionHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0xF8 ||  // CLC - Clear Carry Flag
		opcode == 0xF9 ||  // STC - Set Carry Flag
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

			case 0xFC: // CLD
					   // Direction flag not modeled in this emulator
				break;

			case 0xFD: // STD
					   // Direction flag not modeled in this emulator
				break;
		}

		// Advance EIP
		core.Registers["eip"] += 1;
	}
}
