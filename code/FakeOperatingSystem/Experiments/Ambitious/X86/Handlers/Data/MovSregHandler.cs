namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// 0x8C — MOV r/m16, Sreg   (store segment register to r/m16)
/// 0x8E — MOV Sreg, r/m16   (load segment register from r/m16)
/// In flat 32-bit protected mode, segment registers are effectively fixed.
/// We stub these: 8C writes 0 (or the flat-mode selector) to destination; 8E is a NOP.
/// </summary>
public class MovSregHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x8C || opcode == 0x8E;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		byte modrm  = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte sreg = (byte)((modrm >> 3) & 0x7);
		byte rm   = (byte)(modrm & 0x7);

		uint instrLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );

		if ( opcode == 0x8C ) // MOV r/m16, Sreg — write a flat-mode selector value
		{
			// Typical flat-mode values: CS=0x1B, DS/ES/SS/FS/GS=0x23
			ushort val = sreg == 1 ? (ushort)0x1B : (ushort)0x23; // 1=CS, others=data seg

			if ( mod == 3 )
			{
				string reg = X86AddressingHelper.GetRegisterName( rm );
				core.Registers[reg] = (core.Registers[reg] & 0xFFFF0000) | val;
			}
			else
			{
				uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				core.WriteByte( addr,     (byte)(val & 0xFF) );
				core.WriteByte( addr + 1, (byte)(val >> 8)   );
			}
		}
		// 0x8E: MOV Sreg, r/m16 — NOP (we ignore segment register loads)

		core.Registers["eip"] += instrLen;
	}
}
