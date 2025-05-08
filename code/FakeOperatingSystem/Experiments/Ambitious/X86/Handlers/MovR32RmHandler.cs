using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

// MOV r32, r/m32 (0x8B)
public class MovR32RmHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x8B;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );

		if ( mod == 3 ) // Register to register
		{
			string sourceReg = X86AddressingHelper.GetRegisterName( rm );
			core.Registers[destReg] = core.Registers[sourceReg];
			core.Registers["eip"] += 2;
		}
		else // Memory source
		{
			// Use the helper to calculate effective address (supports SIB)
			uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			core.Registers[destReg] = core.ReadDword( effectiveAddress );

			// Use the helper to determine instruction length
			uint length = X86AddressingHelper.GetInstructionLength( modrm );
			core.Registers["eip"] += length;
		}
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
