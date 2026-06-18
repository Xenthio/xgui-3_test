namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0xA0 — MOV AL, [moffs32]  (byte load from absolute address)
/// 0xA2 — MOV [moffs32], AL (byte store to absolute address)
public class MovAlMoffs32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xA0 || opcode == 0xA2;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		uint addr = core.ReadDword( eip + 1 );
		if ( opcode == 0xA0 ) // MOV AL, [moffs32]
		{
			byte val = core.ReadByte( addr );
			core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | val;
		}
		else // 0xA2: MOV [moffs32], AL
		{
			core.WriteByte( addr, (byte)(core.Registers["eax"] & 0xFF) );
		}
		core.Registers["eip"] += 5;
	}
}
