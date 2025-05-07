namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class OperandSizePrefixHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x66;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];

		// Skip the prefix byte
		core.Registers["eip"]++;

		// Look at the next byte - often this prefix is used with common instructions
		byte nextByte = core.ReadByte( eip + 1 );

		// Handle common combinations
		switch ( nextByte )
		{
			case 0x89: // MOV r/m16, r16 (16-bit version of MOV r/m32, r32)
				Handle66_MOV_Rm16_R16( core );
				break;

			case 0x8B: // MOV r16, r/m16 (16-bit version of MOV r32, r/m32)
				Handle66_MOV_R16_Rm16( core );
				break;

			case 0x0F: // Two-byte opcode with operand size prefix
				Handle66_0F_Prefix( core );
				break;

			case 0x83: // Immediate Group 1 with sign-extended imm8 to 16-bit
			case 0x81: // Immediate Group 1 with 16-bit immediate
				Handle66_Immediate_Group1( core, nextByte );
				break;

			default:
				// For unhandled combinations, we'll log and skip the instruction
				Log.Warning( $"Unhandled 0x66 prefix combination: 0x66 0x{nextByte:X2}" );
				// Skip the next byte too (the instruction after the prefix)
				core.Registers["eip"]++;
				break;
		}
	}

	private void Handle66_MOV_Rm16_R16( X86Core core )
	{
		// Similar to MOV r/m32, r32 but operates on 16-bit registers
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );

		// For simplicity in this stub, just skip the instruction
		// In a full implementation, you would handle the 16-bit MOV
		core.Registers["eip"] += 2; // Skip opcode and modrm
		Log.Info( "16-bit MOV r/m16, r16 (stub implementation)" );
	}

	private void Handle66_MOV_R16_Rm16( X86Core core )
	{
		// Similar to MOV r32, r/m32 but operates on 16-bit registers
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );

		// For simplicity in this stub, just skip the instruction
		core.Registers["eip"] += 2; // Skip opcode and modrm
		Log.Info( "16-bit MOV r16, r/m16 (stub implementation)" );
	}

	private void Handle66_0F_Prefix( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte thirdByte = core.ReadByte( eip + 2 );

		// This is a 3-byte instruction sequence: 0x66 0x0F <op>
		// Common for SSE/SSE2 instructions with 16-bit operand size override

		switch ( thirdByte )
		{
			case 0x29: // MOVAPS - Move Aligned Packed Single-Precision
			case 0x7F: // MOVDQA - Move Aligned Double Quadword
				core.Registers["eip"] += 3; // Skip the 3-byte opcode
				Log.Info( $"16-bit SSE instruction: 0x66 0x0F 0x{thirdByte:X2} (stub implementation)" );
				break;

			default:
				Log.Warning( $"Unhandled 16-bit SSE instruction: 0x66 0x0F 0x{thirdByte:X2}" );
				core.Registers["eip"] += 3;
				break;
		}
	}

	private void Handle66_Immediate_Group1( X86Core core, byte opcode )
	{
		uint eip = core.Registers["eip"];

		// These are like the 32-bit versions but operate on 16-bit operands
		// For now, we'll just skip them
		if ( opcode == 0x83 ) // With 8-bit sign-extended immediate
			core.Registers["eip"] += 3; // opcode + modrm + imm8
		else // With 16-bit immediate
			core.Registers["eip"] += 4; // opcode + modrm + imm16

		Log.Info( $"16-bit Group 1 immediate instruction: 0x66 0x{opcode:X2} (stub implementation)" );
	}
}

