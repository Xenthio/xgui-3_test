namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovReg8SSRm8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x12;

	public void Execute( X86Core core )
	{
		// This is MOV reg8, SS:[r/m8] (rare, legacy)
		// For Win32, you can treat as NOP or log and advance EIP
		core.LogVerbose( "MOV reg8, SS:[r/m8] encountered (opcode 0x12) - treating as NOP" );
		core.Registers["eip"] += 2; // Advance by 2 bytes (opcode + modrm)
	}
}
