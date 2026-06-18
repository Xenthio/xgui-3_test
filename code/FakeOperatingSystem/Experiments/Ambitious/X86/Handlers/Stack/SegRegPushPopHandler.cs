namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// Segment register PUSH/POP instructions.
/// In flat 32-bit protected mode, segment registers are fixed.
/// PUSH Sreg pushes the selector value (0x23 for data, 0x1B for code).
/// POP Sreg is ignored (we can't change segment regs in flat mode).
/// </summary>
public class SegRegPushPopHandler : IInstructionHandler
{
	// 0x06=PUSH ES, 0x07=POP ES, 0x0E=PUSH CS
	// 0x16=PUSH SS, 0x17=POP SS
	// 0x1E=PUSH DS, 0x1F=POP DS
	public bool CanHandle( byte opcode ) =>
		opcode == 0x06 || opcode == 0x07 ||
		opcode == 0x0E ||
		opcode == 0x16 || opcode == 0x17 ||
		opcode == 0x1E || opcode == 0x1F;

	public void Execute( X86Core core )
	{
		byte opcode = core.ReadByte( core.Registers["eip"] );

		switch ( opcode )
		{
			case 0x06: // PUSH ES
			case 0x0E: // PUSH CS
			case 0x16: // PUSH SS
			case 0x1E: // PUSH DS
				// Push a flat-mode selector (data=0x23, code=0x1B)
				core.Push( opcode == 0x0E ? 0x1Bu : 0x23u );
				break;

			case 0x07: // POP ES
			case 0x17: // POP SS
			case 0x1F: // POP DS
				// Pop and discard (we can't change segment regs)
				core.Pop();
				break;
		}

		core.Registers["eip"] += 1;
	}
}
