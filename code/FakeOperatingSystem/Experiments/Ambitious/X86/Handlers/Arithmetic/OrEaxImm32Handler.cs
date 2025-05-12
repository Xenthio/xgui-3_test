namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class OrEaxImm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x0D;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		uint imm32 = core.ReadDword( eip + 1 );

		// Perform OR operation on EAX
		core.Registers["eax"] |= imm32;

		// Set flags
		core.ZeroFlag = core.Registers["eax"] == 0;
		core.SignFlag = (core.Registers["eax"] & 0x80000000) != 0;
		core.CarryFlag = false; // OR always clears CF
		core.OverflowFlag = false; // OR always clears OF

		// Advance EIP past opcode and immediate
		core.Registers["eip"] += 5;

		// Log the operation
		core.LogMaths( $"OR eax, {imm32:X8}, result: {core.Registers["eax"]} (EIP: {eip:X8})" );
	}
}
