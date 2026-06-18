namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x34 — XOR AL, imm8
/// 0x35 — XOR EAX, imm32
public class XorAlImmHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x34 || opcode == 0x35;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		if ( opcode == 0x34 )
		{
			byte al  = (byte)(core.Registers["eax"] & 0xFF);
			byte imm = core.ReadByte( eip + 1 );
			byte result = (byte)(al ^ imm);
			core.ZeroFlag     = result == 0;
			core.SignFlag      = (result & 0x80) != 0;
			core.CarryFlag     = false;
			core.OverflowFlag  = false;
			core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | result;
			core.Registers["eip"] += 2;
		}
		else // 0x35 — XOR EAX, imm32
		{
			uint eax = core.Registers["eax"];
			uint imm = core.ReadDword( eip + 1 );
			uint result = eax ^ imm;
			core.ZeroFlag     = result == 0;
			core.SignFlag      = (result & 0x80000000) != 0;
			core.CarryFlag     = false;
			core.OverflowFlag  = false;
			core.Registers["eax"] = result;
			core.Registers["eip"] += 5;
		}
	}
}
