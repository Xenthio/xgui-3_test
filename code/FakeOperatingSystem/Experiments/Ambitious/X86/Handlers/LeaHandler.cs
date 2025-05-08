using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class LeaHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x8D;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// LEA never uses register-to-register (mod=3) mode
		if ( mod == 3 )
		{
			throw new InvalidOperationException( "Invalid LEA instruction: mod=3 is not allowed" );
		}

		// Get destination register
		string destReg = X86AddressingHelper.GetRegisterName( reg );

		// For LEA, we calculate the address but don't dereference it
		// Use our helper to properly handle SIB addressing
		uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );

		// Store the calculated address directly in the destination register
		core.Registers[destReg] = effectiveAddress;

		// Advance EIP using our helper
		uint length = X86AddressingHelper.GetInstructionLength( modrm );
		core.Registers["eip"] += length;
	}

	private string GetRegisterName( int code ) => code switch
	{
		0 => "eax",
		1 => "ecx",
		2 => "edx",
		3 => "ebx",
		4 => "esp",
		5 => "ebp",
		6 => "esi",
		7 => "edi",
		_ => throw new ArgumentException( $"Invalid register code: {code}" )
	};
}
