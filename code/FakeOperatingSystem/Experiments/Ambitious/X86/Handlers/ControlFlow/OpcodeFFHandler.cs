using System;
using System.Linq;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class OpcodeFFHandler : IInstructionHandler
{
	private readonly X86Interpreter _interpreter;

	public OpcodeFFHandler( X86Interpreter interpreter )
	{
		_interpreter = interpreter;
	}

	public bool CanHandle( byte opcode ) => opcode == 0xFF;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte mod = (byte)(modrm >> 6);
		byte rm = (byte)(modrm & 0x7);

		core.LogVerbose( $"OpcodeFFHandler: opcode=0xFF, EIP={eip:X8} modrm=0x{modrm:X2}, reg={reg}, mod={mod}, rm={rm}" );

		if ( reg == 0 ) // INC r/m32
		{
			if ( mod == 3 ) // Register operand
			{
				string destReg = GetRegisterName( rm );
				uint value = core.Registers[destReg];

				// Perform increment
				uint result = value + 1;
				core.Registers[destReg] = result;

				// Set flags (CF not affected by INC)
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80000000) != 0;
				core.OverflowFlag = value == 0x7FFFFFFF; // Overflow if went from max positive to negative

				core.Registers["eip"] += 2;
			}
			else // Memory operand
			{
				uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				uint value = core.ReadDword( effectiveAddress );

				// Perform increment
				uint result = value + 1;
				core.WriteDword( effectiveAddress, result );

				// Set flags (CF not affected by INC)
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80000000) != 0;
				core.OverflowFlag = value == 0x7FFFFFFF; // Overflow if went from max positive to negative

				// Advance EIP
				uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
				core.Registers["eip"] += length;
			}
		}
		else if ( reg == 1 ) // DEC r/m32
		{
			if ( mod == 3 ) // Register operand
			{
				string destReg = GetRegisterName( rm );
				uint value = core.Registers[destReg];

				// Perform decrement
				uint result = value - 1;
				core.Registers[destReg] = result;

				// Set flags (CF not affected by DEC)
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80000000) != 0;
				core.OverflowFlag = value == 0x80000000; // Overflow if went from min negative to positive

				core.Registers["eip"] += 2;
			}
			else // Memory operand
			{
				uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				uint value = core.ReadDword( effectiveAddress );

				// Perform decrement
				uint result = value - 1;
				core.WriteDword( effectiveAddress, result );

				// Set flags (CF not affected by DEC)
				core.ZeroFlag = result == 0;
				core.SignFlag = (result & 0x80000000) != 0;
				core.OverflowFlag = value == 0x80000000; // Overflow if went from min negative to positive

				// Advance EIP
				uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
				core.Registers["eip"] += length;
			}
		}
		else if ( reg == 2 ) // CALL r/m32
		{
			uint target;

			// Calculate target address based on ModRM
			if ( mod == 3 ) // Register operand
			{
				string regName = GetRegisterName( rm );
				target = core.Registers[regName];

				if ( target == 0 )
				{
					_interpreter.HaltWithMessageBox(
						"Fatal Exception",
						$"A fatal exception has occurred in the virtual machine.\n\n" +
						$"Attempted to CALL invalid address in {regName}: 0x{target:X8}\n\n" +
						$"This is usually caused by an uninitialized or corrupted function pointer.\n\n" +
						$"Press OK to terminate the program."
					);
					return;
				}
			}
			else
			{
				uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				target = core.ReadDword( effectiveAddress );

				if ( target == 0 )
				{
					_interpreter.HaltWithMessageBox(
						"Fatal Exception",
						$"A fatal exception has occurred in the virtual machine.\n\n" +
						$"Attempted to CALL invalid address at effective address: 0x{effectiveAddress:X8}\n\n" +
						$"This is usually caused by an uninitialized or corrupted function pointer.\n\n" +
						$"Press OK to terminate the program."
					);
					return;
				}
			}

			// Calculate instruction length
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += length;

			// CALL instruction behavior: push return address, jump to target
			core.Push( core.Registers["eip"] );

			// Track function entry for debugging purposes - not real CPU behavior
			core.EnterFunction( core.Registers["eip"] );

			// API emulation can still work at the emulator level
			var api = _interpreter.Imports.FirstOrDefault( x => x.Value == target );
			if ( api.Key != null )
			{
				// API emulation - emulator specific
				core.LogVerbose( $"OpcodeFFHandler: Detected API call to {api.Key}" );
				bool handled = false;

				// SAVE the return address BEFORE API call
				uint returnAddress = core.Registers["eip"];

				foreach ( var emu in _interpreter.APIEmulators )
				{
					if ( emu.TryCall( api.Key, core, _interpreter, out var result ) )
					{
						// these should be handled by the api's calling convention 
						//core.Registers["eax"] = result;
						// Use our saved return address
						//core.Registers["eip"] = returnAddress;

						//Log.Info( $"OpcodeFFHandler: Set EIP to 0x{returnAddress:X8}" );
						handled = true;
						break;
					}
				}

				// If no emulator could handle this API call
				if ( !handled )
				{
					APIEmulator.ReportMissingExport( _interpreter, api.Key );
					core.Registers["eax"] = 0; // Default return value

					// Use our saved return address
					core.Registers["eip"] = returnAddress;
				}

				// Log the API call
				core.ExitFunction();

				return;
			}
			else
			{
				// Normal CALL - just jump to target
				core.Registers["eip"] = target;
			}
		}
		else if ( reg == 4 ) // JMP r/m32
		{
			uint target;
			if ( mod == 3 ) // Register operand
			{
				string regName = GetRegisterName( rm );
				target = core.Registers[regName];
			}
			else // Memory operand
			{
				uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				target = core.ReadDword( effectiveAddress );
			}

			// Calculate instruction length
			uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );

			// Advance EIP to after the instruction
			core.Registers["eip"] += length;

			// Check for API call
			var api = _interpreter.Imports.FirstOrDefault( x => x.Value == target );
			if ( api.Key != null )
			{
				core.LogVerbose( $"OpcodeFFHandler: Detected JMP to API {api.Key}" );
				bool handled = false;
				foreach ( var emu in _interpreter.APIEmulators )
				{
					if ( emu.TryCall( api.Key, core, _interpreter, out var result, isJump: true ) )
					{
						handled = true;
						break;
					}
				}
				if ( !handled )
				{
					APIEmulator.ReportMissingExport( _interpreter, api.Key );
					core.Registers["eax"] = 0;
				}
				// After API call, EIP should be set to the return address (simulate RET)
				// You may want to pop the return address from the stack if needed, or just halt.
				// For JMP, there is no return, so you may want to set EIP to 0xFFFFFFFF to halt.
				//core.Registers["eip"] = 0xFFFFFFFF;
				return;
			}
			else
			{
				// Normal JMP
				core.Registers["eip"] = target;
			}
		}
		else if ( reg == 6 ) // PUSH r/m32
		{
			uint value;

			if ( mod == 3 ) // Register operand
			{
				string regName = GetRegisterName( rm );
				value = core.Registers[regName];
				core.Registers["eip"] += 2;
			}
			else // Memory operand
			{
				uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				value = core.ReadDword( effectiveAddress );
				uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
				core.Registers["eip"] += length;
			}

			// Push the value onto the stack
			core.Push( value );
		}
		/*		else if ( reg == 7 ) // Technically undefined in standard x86
				{
					if ( mod == 3 && rm == 7 && modrm == 0xFF ) // FF FF pattern
					{
						// This specific pattern (0xFF 0xFF) appears to be used in Windows executables
						// and is executed without error on real CPUs. Treat as NOP.
						Log.Warning( $"Handling undefined instruction 0xFF 0xFF (FF /7 EDI) as NOP at {eip:X8}" );
						core.Registers["eip"] += 2;
					}
					else
					{
						// Other FF /7 variants - also treat as NOP but log differently
						Log.Warning( $"Encountered unusual instruction: 0xFF /7 (modrm=0x{modrm:X2}). Treating as NOP." );

						if ( mod == 3 ) // Register operand
						{
							core.Registers["eip"] += 2;
						}
						else // Memory operand
						{
							uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
							core.Registers["eip"] += length;
						}
					}
				}*/
		else
		{
			throw new InvalidOperationException( $"Unimplemented 0xFF /{reg} (modrm=0x{modrm:X2}, mod={mod}, rm={rm})" );
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
		_ => throw new Exception( $"Invalid register code: {code}" )
	};
}
