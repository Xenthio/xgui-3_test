namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovMoffs32EaxHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xA3;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		bool is16bit = false;

		// Check for 0x66 prefix (operand size override)
		if ( eip > 0 && core.ReadByte( eip - 1 ) == 0x66 )
			is16bit = true;

		uint address = core.ReadDword( eip + 1 );

		if ( is16bit )
		{
			ushort value = (ushort)(core.Registers["eax"] & 0xFFFF);
			core.WriteWord( address, value );
			core.Registers["eip"] += 5; // 0x66 + 0xA3 + 4-byte address
		}
		else
		{
			uint value = core.Registers["eax"];
			core.WriteDword( address, value );
			core.Registers["eip"] += 5; // 0xA3 + 4-byte address
		}
	}
}
