using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

/// <summary>
/// Helper class for calculating x86 memory addressing modes
/// </summary>
public static class X86AddressingHelper
{
	/// <summary>
	/// Calculate effective address with support for SIB byte
	/// </summary>
	public static uint CalculateEffectiveAddress( X86Core core, byte modrm, uint instructionAddress )
	{
		byte mod = (byte)(modrm >> 6);
		byte rm = (byte)(modrm & 0x7);

		// Special case - disp32 only addressing
		if ( mod == 0 && rm == 5 )
			return core.ReadDword( instructionAddress + 2 );

		uint ea = 0;
		uint sibOffset = 0; // Additional offset from SIB byte

		core.LogVerbose( $"CalculateEffectiveAddress: EIP=0x{instructionAddress:X8}, ESP=0x{core.Registers["esp"]:X8}, modrm=0x{modrm:X2}, mod={mod}, rm={rm}" );

		if ( rm == 4 ) // SIB byte present
		{
			byte sib = core.ReadByte( instructionAddress + 2 );
			sibOffset = 1;

			byte scale = (byte)((sib >> 6) & 0x3);
			byte index = (byte)((sib >> 3) & 0x7);
			byte base_ = (byte)(sib & 0x7);

			if ( base_ == 5 && mod == 0 )
			{
				// [index*scale + disp32]
				ea = core.ReadDword( instructionAddress + 3 );
				sibOffset = 5; // SIB + disp32
				if ( index != 4 )
				{
					uint indexValue = core.Registers[GetRegisterName( index )];
					uint scaleFactor = (uint)(1 << scale);
					ea += indexValue * scaleFactor;
				}
			}
			else
			{
				// [base][+ index*scale][+ disp]
				ea = core.Registers[GetRegisterName( base_ )];
				if ( index != 4 )
				{
					uint indexValue = core.Registers[GetRegisterName( index )];
					uint scaleFactor = (uint)(1 << scale);
					ea += indexValue * scaleFactor;
				}
			}
			core.LogVerbose( $"[EA] SIB/ESP: EIP=0x{instructionAddress:X8}, ESP=0x{core.Registers["esp"]:X8}, base={base_}, index={index}, scale={scale}, mod={mod}, sibOffset={sibOffset}, ea=0x{ea:X8}" );
		}
		else // Standard addressing mode
		{
			// Get base register value for non-SIB addressing
			ea = core.Registers[GetRegisterName( rm )];
		}

		// Add displacement if present
		if ( mod == 1 ) // 8-bit displacement
		{
			sbyte disp8 = (sbyte)core.ReadByte( instructionAddress + 2 + sibOffset );
			ea += (uint)disp8;
		}
		else if ( mod == 2 ) // 32-bit displacement
		{
			uint disp32 = core.ReadDword( instructionAddress + 2 + sibOffset );
			ea += disp32;
		}

		return ea;
	}

	/// <summary>
	/// Calculate instruction length based on ModR/M and potential SIB
	/// </summary>
	public static uint GetInstructionLength( byte modrm )
	{
		byte mod = (byte)(modrm >> 6);
		byte rm = (byte)(modrm & 0x7);

		// Base size: 1 byte opcode + 1 byte ModR/M
		uint size = 2;

		// SIB byte?
		if ( rm == 4 && mod != 3 )
			size++;

		// Displacement?
		if ( mod == 1 ) // 8-bit displacement
			size++;
		else if ( mod == 2 || (mod == 0 && rm == 5) ) // 32-bit displacement
			size += 4;

		return size;
	}

	/// <summary>
	/// Calculate instruction length based on ModR/M and potential SIB
	/// </summary>
	public static uint GetInstructionLength( byte modrm, X86Core core, uint instructionAddress )
	{
		byte mod = (byte)(modrm >> 6);
		byte rm = (byte)(modrm & 0x7);

		uint size = 2; // opcode + modrm

		if ( rm == 4 && mod != 3 )
		{
			size++; // SIB
			byte sib = core.ReadByte( instructionAddress + 2 );
			byte base_ = (byte)(sib & 0x7);

			// Special case: SIB with base=5 and mod=0 means disp32 follows SIB
			if ( base_ == 5 && mod == 0 )
				size += 4;
		}

		// Displacement
		if ( mod == 1 )
			size++;
		else if ( mod == 2 || (mod == 0 && rm == 5) )
			size += 4;

		return size;
	}

	public static string GetRegisterName( int code ) => code switch
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

