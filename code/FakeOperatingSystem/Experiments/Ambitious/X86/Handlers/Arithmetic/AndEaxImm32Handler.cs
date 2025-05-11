namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class AndEaxImm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x25;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		uint imm32 = core.ReadDword( eip + 1 );
		uint result = core.Registers["eax"] & imm32;
		core.Registers["eax"] = result;

		// Set flags (simplified)
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.CarryFlag = false;
		core.OverflowFlag = false;

		core.Registers["eip"] += 5; // 1 byte opcode + 4 bytes immediate
	}
}
