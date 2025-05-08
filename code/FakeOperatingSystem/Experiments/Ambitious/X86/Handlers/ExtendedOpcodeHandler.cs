using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class ExtendedOpcodeHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x0F;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte secondByte = core.ReadByte( eip + 1 );

		// First handle the common conditional jumps (0x0F 0x8X)
		if ( secondByte >= 0x80 && secondByte <= 0x8F )
		{
			HandleConditionalJump32( core, secondByte );
			return;
		}

		switch ( secondByte )
		{
			case 0x31: // RDTSC - Read Time-Stamp Counter
					   // Simple stub - just return a dummy value
				core.Registers["edx"] = 0;
				core.Registers["eax"] = 0x12345678;
				core.Registers["eip"] += 2;
				break;

			case 0x45: // CMOVNE r16, r/m16 - Conditional move if not equal/not zero
					   // This is a conditional move instruction that moves only if ZF=0
				core.Registers["eip"] += 3; // Skip the instruction
				Log.Info( "16-bit CMOVNE (0x66 0x0F 0x45) instruction (stub implementation)" );
				break;

			case 0x4E: // CMOVLE r32, r/m32 - Conditional Move if Less or Equal
				HandleConditionalMove( core, secondByte, (core.ZeroFlag || (core.SignFlag != core.OverflowFlag)) );
				break;

			case 0x57: // XORPS xmm1, xmm2/m128
					   // This is an SSE instruction that XORs packed single-precision values
					   // For most applications, we can just skip it as it's often used for initialization
				Log.Info( "Handling XORPS (SSE instruction) - stub implementation" );
				byte xorpsMod = core.ReadByte( eip + 2 );
				core.Registers["eip"] += 3; // Skip opcode (2 bytes) + ModR/M byte
				break;

			case 0xA2: // CPUID - CPU Identification
					   // Return basic values
				switch ( core.Registers["eax"] )
				{
					case 0: // Max function & vendor ID
						core.Registers["eax"] = 1;
						core.Registers["ebx"] = 0x756E6547; // "Genu"
						core.Registers["edx"] = 0x49656E69; // "ineI"
						core.Registers["ecx"] = 0x6C65746E; // "ntel"
						break;
					case 1: // Feature flags
						core.Registers["eax"] = 0x00000633; // Family 6, Model 3, Stepping 3
						core.Registers["ebx"] = 0;
						core.Registers["ecx"] = 0;
						core.Registers["edx"] = 0x00000001; // FPU present (just a dummy value)
						break;
					default:
						core.Registers["eax"] = 0;
						core.Registers["ebx"] = 0;
						core.Registers["ecx"] = 0;
						core.Registers["edx"] = 0;
						break;
				}
				core.Registers["eip"] += 2;
				break;

			case 0xB6: // MOVZX r32, r/m8
			case 0xB7: // MOVZX r32, r/m16
				HandleMovzx( core, secondByte );
				break;

			case 0xBE: // MOVSX r32, r/m8
			case 0xBF: // MOVSX r32, r/m16
				HandleMovsx( core, secondByte );
				break;

			case 0xAF: // IMUL r32, r/m32
				HandleImul( core );
				break;

			default:
				Log.Warning( $"Unimplemented extended opcode: 0x0F 0x{secondByte:X2}" );
				core.Registers["eip"] += 2; // Skip the unimplemented instruction
				break;
		}
	}

	private void HandleConditionalJump32( X86Core core, byte opcode )
	{
		uint eip = core.Registers["eip"];
		int offset = (int)core.ReadDword( eip + 2 );
		bool condition = EvaluateCondition( opcode - 0x80, core );

		if ( condition )
			core.Registers["eip"] = (uint)((int)eip + 6 + offset); // 2 bytes opcode + 4 bytes offset
		else
			core.Registers["eip"] += 6; // Skip if not taken
	}

	private bool EvaluateCondition( int condCode, X86Core core )
	{
		switch ( condCode )
		{
			case 0x4: return core.ZeroFlag;                  // JE/JZ
			case 0x5: return !core.ZeroFlag;                 // JNE/JNZ
			case 0x2: return core.CarryFlag;                 // JB/JNAE/JC
			case 0x3: return !core.CarryFlag;                // JAE/JNB/JNC
			case 0x6: return core.ZeroFlag || core.CarryFlag; // JBE/JNA
			case 0x7: return !core.ZeroFlag && !core.CarryFlag; // JA/JNBE
			case 0x8: return core.SignFlag;                  // JS
			case 0x9: return !core.SignFlag;                 // JNS
			case 0xC: return core.SignFlag != core.OverflowFlag; // JL/JNGE
			case 0xD: return core.SignFlag == core.OverflowFlag; // JGE/JNL
			case 0xE: return core.ZeroFlag || (core.SignFlag != core.OverflowFlag); // JLE/JNG
			case 0xF: return !core.ZeroFlag && (core.SignFlag == core.OverflowFlag); // JG/JNLE
			default: return false;
		}
	}

	private void HandleMovzx( X86Core core, byte opcode )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = GetRegisterName( reg );

		if ( mod == 3 ) // Register operand
		{
			string sourceReg = GetRegisterName( rm );
			if ( opcode == 0xB6 ) // MOVZX r32, r/m8
			{
				// Zero-extend the low byte
				core.Registers[destReg] = core.Registers[sourceReg] & 0xFF;
			}
			else // MOVZX r32, r/m16
			{
				// Zero-extend the low word
				core.Registers[destReg] = core.Registers[sourceReg] & 0xFFFF;
			}
			core.Registers["eip"] += 3;
		}
		else
		{
			// Handle memory operands if needed
			Log.Warning( "MOVZX with memory operand not implemented" );
			core.Registers["eip"] += 3;
		}
	}

	private void HandleMovsx( X86Core core, byte opcode )
	{
		// Similar to MOVZX but with sign extension
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		// Simplified stub
		core.Registers["eip"] += 3;
	}

	private void HandleImul( X86Core core )
	{
		// Simplified stub for IMUL instruction
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		if ( mod == 3 ) // Register operand
		{
			string destReg = GetRegisterName( reg );
			string sourceReg = GetRegisterName( rm );

			int result = (int)core.Registers[destReg] * (int)core.Registers[sourceReg];
			core.Registers[destReg] = (uint)result;

			// Set flags
			core.ZeroFlag = result == 0;
			core.SignFlag = (result < 0);

			core.Registers["eip"] += 3;
		}
		else
		{
			// Handle memory operands if needed
			Log.Warning( "IMUL with memory operand not implemented" );
			core.Registers["eip"] += 3;
		}
	}

	private void HandleConditionalMove( X86Core core, byte opcode, bool condition )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 2 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );

		if ( condition )
		{
			if ( mod == 3 ) // Register source
			{
				string sourceReg = X86AddressingHelper.GetRegisterName( rm );
				core.Registers[destReg] = core.Registers[sourceReg];
			}
			else // Memory source
			{
				uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				core.Registers[destReg] = core.ReadDword( effectiveAddress );
			}
		}

		// Advance EIP regardless of whether the condition was true
		if ( mod == 3 )
		{
			core.Registers["eip"] += 3;
		}
		else
		{
			uint length = X86AddressingHelper.GetInstructionLength( modrm );
			core.Registers["eip"] += 2 + length;
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
