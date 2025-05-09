using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class LesHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xC4;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// LES cannot have a register source operand
		if ( mod == 3 )
		{
			throw new InvalidOperationException( "Invalid LES instruction: mod=3 is not allowed" );
		}

		// Get destination register
		string destReg = X86AddressingHelper.GetRegisterName( reg );

		// Calculate address of far pointer
		uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );

		// Load 32-bit offset part into the register
		uint offset = core.ReadDword( effectiveAddress );
		core.Registers[destReg] = offset;

		// Load 16-bit segment part into ES
		// Since our emulator uses a flat memory model, we'll ignore the segment part
		// but we'll log it for debugging
		uint segment = core.ReadDword( effectiveAddress + 4 ) & 0xFFFF;
		Log.Info( $"LES: Loaded far pointer {segment:X4}:{offset:X8} - Segment part ignored in flat memory model" );

		// Advance EIP
		uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		core.Registers["eip"] += length;
	}
}

