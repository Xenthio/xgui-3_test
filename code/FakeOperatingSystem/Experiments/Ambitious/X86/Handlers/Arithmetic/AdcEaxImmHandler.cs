namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x14 — ADC AL, imm8
/// 0x15 — ADC EAX, imm32
public class AdcEaxImmHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x14 || opcode == 0x15;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		uint carry = core.CarryFlag ? 1u : 0u;

		if ( opcode == 0x14 )
		{
			byte al  = (byte)(core.Registers["eax"] & 0xFF);
			byte imm = core.ReadByte( eip + 1 );
			uint res = (uint)al + imm + carry;
			core.CarryFlag = res > 0xFF;
			core.OverflowFlag = (~(al ^ imm) & (al ^ (byte)res) & 0x80) != 0;
			byte result = (byte)(res & 0xFF);
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80) != 0;
			core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | result;
			core.Registers["eip"] += 2;
		}
		else // 0x15 — ADC EAX, imm32
		{
			uint eax = core.Registers["eax"];
			uint imm = core.ReadDword( eip + 1 );
			ulong res = (ulong)eax + imm + carry;
			core.CarryFlag = res > 0xFFFFFFFF;
			core.OverflowFlag = (~(eax ^ imm) & (eax ^ (uint)res) & 0x80000000) != 0;
			uint result = (uint)(res & 0xFFFFFFFF);
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80000000) != 0;
			core.Registers["eax"] = result;
			core.Registers["eip"] += 5;
		}
	}
}
