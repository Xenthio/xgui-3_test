namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class TestEaxImm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0xA8 || // TEST AL, imm8
		opcode == 0xA9;   // TEST EAX, imm32

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		if ( opcode == 0xA8 ) // TEST AL, imm8
		{
			byte al = (byte)(core.Registers["eax"] & 0xFF);
			byte imm8 = core.ReadByte( eip + 1 );

			// Perform the AND but don't store the result
			byte result = (byte)(al & imm8);

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.CarryFlag = false; // Always cleared
			core.OverflowFlag = false; // Always cleared

			core.Registers["eip"] += 2;
		}
		else // TEST EAX, imm32
		{
			uint eax = core.Registers["eax"];
			uint imm32 = core.ReadDword( eip + 1 );

			// Perform the AND but don't store the result
			uint result = eax & imm32;

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80000000) != 0;
			core.CarryFlag = false; // Always cleared
			core.OverflowFlag = false; // Always cleared

			core.Registers["eip"] += 5; // Opcode (1) + Immediate (4)
		}

		Log.Info( $"TEST instruction: ZF={core.ZeroFlag}, SF={core.SignFlag}" );
	}
}
