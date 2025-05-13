namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class CmpEaxImm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x3D;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		uint imm32 = core.ReadDword( eip + 1 );
		uint eax = core.Registers["eax"];
		uint result = eax - imm32;

		// Set flags as per x86 semantics
		core.ZeroFlag = (result == 0);
		core.SignFlag = ((result & 0x80000000) != 0);
		core.CarryFlag = (eax < imm32);
		core.OverflowFlag = (((eax ^ imm32) & (eax ^ result) & 0x80000000) != 0);

		// Advance EIP
		core.Registers["eip"] += 5;

		if ( X86Core.VerboseLogging )
		{
			core.LogVerbose( $"CMP EAX, 0x{imm32:X8} (EAX=0x{eax:X8}) -> result=0x{result:X8} ZF={core.ZeroFlag} SF={core.SignFlag} CF={core.CarryFlag} OF={core.OverflowFlag}" );
		}
	}
}
