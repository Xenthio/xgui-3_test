namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// 0x98 — CWDE: sign-extend AX into EAX (AX → EAX, 32-bit mode)
/// 0x99 — CDQ:  sign-extend EAX into EDX:EAX (if EAX is negative, EDX = 0xFFFFFFFF)
/// </summary>
public class SignExtendHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x98 || opcode == 0x99;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		if ( opcode == 0x98 ) // CWDE: sign-extend AX (low 16 bits of EAX) into EAX
		{
			short ax = (short)(core.Registers["eax"] & 0xFFFF);
			core.Registers["eax"] = (uint)(int)ax; // sign-extend to 32 bits
		}
		else // 0x99: CDQ — sign-extend EAX into EDX:EAX
		{
			if ( (core.Registers["eax"] & 0x80000000) != 0 )
				core.Registers["edx"] = 0xFFFFFFFF;
			else
				core.Registers["edx"] = 0;
		}

		core.Registers["eip"]++;
	}
}
