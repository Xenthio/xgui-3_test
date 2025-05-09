namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class LoopHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0xE0 || // LOOPNE/LOOPNZ rel8
		opcode == 0xE1 || // LOOPE/LOOPZ rel8
		opcode == 0xE2;   // LOOP rel8

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		// Get the signed 8-bit displacement
		sbyte displacement = (sbyte)core.ReadByte( eip + 1 );

		// Decrement ECX first (this happens for all LOOP variants)
		core.Registers["ecx"]--;

		bool shouldJump = false;

		switch ( opcode )
		{
			case 0xE0: // LOOPNE/LOOPNZ - Loop if ECX != 0 and ZF=0
				shouldJump = core.Registers["ecx"] != 0 && !core.ZeroFlag;
				break;

			case 0xE1: // LOOPE/LOOPZ - Loop if ECX != 0 and ZF=1
				shouldJump = core.Registers["ecx"] != 0 && core.ZeroFlag;
				break;

			case 0xE2: // LOOP - Loop if ECX != 0
				shouldJump = core.Registers["ecx"] != 0;
				break;
		}

		if ( shouldJump )
		{
			// Calculate target address (EIP + displacement + instruction length)
			core.Registers["eip"] = eip + 2 + (uint)displacement;
		}
		else
		{
			// Skip the instruction
			core.Registers["eip"] += 2;
		}

		Log.Info( $"LOOP instruction: ECX={core.Registers["ecx"]}, Jumped={shouldJump}" );
	}
}
