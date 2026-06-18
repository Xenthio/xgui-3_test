namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class FlagInstructionHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0xF8 ||  // CLC - Clear Carry Flag
		opcode == 0xF9 ||  // STC - Set Carry Flag
		opcode == 0xFA ||  // CLI - Clear Interrupt Flag
		opcode == 0xFB ||  // STI - Set Interrupt Flag
		opcode == 0xFC ||  // CLD - Clear Direction Flag
		opcode == 0xFD ||  // STD - Set Direction Flag
		opcode == 0x9C ||  // PUSHFD - Push EFLAGS
		opcode == 0x9D ||  // POPFD  - Pop EFLAGS
		opcode == 0x9E ||  // SAHF   - Store AH into Flags
		opcode == 0x9F;    // LAHF   - Load Flags into AH

	// Build EFLAGS dword from current flag state
	private static uint GetEFlags( X86Core core )
	{
		uint eflags = 0x00000002; // bit 1 always 1 in EFLAGS
		if ( core.CarryFlag )     eflags |= 0x0001;
		if ( core.ParityFlag )    eflags |= 0x0004;
		// AuxiliaryCarryFlag bit 4 — not tracked, leave 0
		if ( core.ZeroFlag )      eflags |= 0x0040;
		if ( core.SignFlag )      eflags |= 0x0080;
		if ( core.InterruptFlag ) eflags |= 0x0200;
		if ( core.DirectionFlag ) eflags |= 0x0400;
		if ( core.OverflowFlag )  eflags |= 0x0800;
		return eflags;
	}

	private static void SetEFlags( X86Core core, uint eflags )
	{
		core.CarryFlag     = (eflags & 0x0001) != 0;
		core.ParityFlag    = (eflags & 0x0004) != 0;
		core.ZeroFlag      = (eflags & 0x0040) != 0;
		core.SignFlag      = (eflags & 0x0080) != 0;
		core.InterruptFlag = (eflags & 0x0200) != 0;
		core.DirectionFlag = (eflags & 0x0400) != 0;
		core.OverflowFlag  = (eflags & 0x0800) != 0;
	}

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

			case 0x9C: // PUSHFD — push 32-bit EFLAGS
				core.Push( GetEFlags( core ) );
				break;

			case 0x9D: // POPFD — pop 32-bit EFLAGS
				SetEFlags( core, core.Pop() );
				break;

			case 0x9E: // SAHF — Store AH into Flags (low 8 bits of EFLAGS)
			{
				uint ah = (core.Registers["eax"] >> 8) & 0xFF;
				core.CarryFlag  = (ah & 0x01) != 0;
				core.ParityFlag = (ah & 0x04) != 0;
				core.ZeroFlag   = (ah & 0x40) != 0;
				core.SignFlag   = (ah & 0x80) != 0;
				break;
			}

			case 0x9F: // LAHF — Load Flags into AH
			{
				uint flags8 = 0x02; // bit 1 always 1
				if ( core.CarryFlag )  flags8 |= 0x01;
				if ( core.ParityFlag ) flags8 |= 0x04;
				if ( core.ZeroFlag )   flags8 |= 0x40;
				if ( core.SignFlag )   flags8 |= 0x80;
				uint eax = core.Registers["eax"] & 0xFFFF00FF;
				core.Registers["eax"] = eax | (flags8 << 8);
				break;
			}
		}

		// Advance EIP
		core.Registers["eip"] += 1;
	}
}
