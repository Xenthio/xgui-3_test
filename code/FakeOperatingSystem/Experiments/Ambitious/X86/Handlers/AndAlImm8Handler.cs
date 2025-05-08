namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class AndAlImm8Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x24;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];

		// Get the immediate 8-bit value
		byte imm8 = core.ReadByte( eip + 1 );

		// Get current AL value (lowest byte of EAX)
		uint eax = core.Registers["eax"];
		byte al = (byte)(eax & 0xFF);

		// Perform the AND operation
		byte result = (byte)(al & imm8);

		// Update AL in EAX (preserve the other bytes)
		core.Registers["eax"] = (eax & 0xFFFFFF00) | result;

		// Set flags
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80) != 0;
		core.CarryFlag = false; // Always cleared
		core.OverflowFlag = false; // Always cleared

		Log.Info( $"AND AL, 0x{imm8:X2}: AL=0x{al:X2} AND 0x{imm8:X2} = 0x{result:X2}, flags: ZF={core.ZeroFlag}, SF={core.SignFlag}" );

		// Advance EIP past opcode and immediate
		core.Registers["eip"] += 2;
	}
}
