namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// Minimal x87 FPU stub handler for opcodes 0xD8-0xDF.
/// We don't emulate the FPU stack; we just skip the instructions cleanly.
/// Winmine only uses basic FPU ops (FILD, FDIV, FISTP) for timing/score display.
/// </summary>
public class FpuStubHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode >= 0xD8 && opcode <= 0xDF || opcode == 0x9B || opcode == 0x9E || opcode == 0x9F;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		// 0x9B = FWAIT — 1 byte, just sync FPU (NOP for us)
		// 0x9E = SAHF — store AH into flags; 0x9F = LAHF — load flags into AH
		if ( opcode == 0x9B || opcode == 0x9F )
		{
			core.Registers["eip"] += 1;
			return;
		}

		if ( opcode == 0x9E ) // SAHF: load SF/ZF/AF/PF/CF from AH
		{
			byte ah = (byte)((core.Registers["eax"] >> 8) & 0xFF);
			core.SignFlag    = (ah & 0x80) != 0;
			core.ZeroFlag   = (ah & 0x40) != 0;
			// AF = bit 4
			core.ParityFlag  = (ah & 0x04) != 0;
			core.CarryFlag   = (ah & 0x01) != 0;
			core.Registers["eip"] += 1;
			return;
		}

		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte rm = (byte)(modrm & 0x7);

		// For register-direct FPU ops (mod==3), instruction is 2 bytes
		// For memory FPU ops, instruction is 2 + modrm-extension bytes
		if ( mod == 3 )
		{
			core.Registers["eip"] += 2;
		}
		else
		{
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
		}

		core.LogVerbose( $"FPU stub: opcode=0x{opcode:X2} modrm=0x{modrm:X2} (skipped)" );
	}
}
