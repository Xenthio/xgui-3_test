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
				string destReg = X86AddressingHelper.GetRegisterName( rm );
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
				string destReg = X86AddressingHelper.GetRegisterName( rm );
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
				string regName = X86AddressingHelper.GetRegisterName( rm );
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

					// Pop the return address that CALL pushed — stdcall callee cleans stack,
					// so we must at minimum pop the ret addr, else the next RET in caller
					// pops a stale value. We can't know param count so just pop ret addr.
					core.Registers["esp"] += 4;

					// Resume at the instruction after the CALL
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
				string regName = X86AddressingHelper.GetRegisterName( rm );
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
				uint eipBeforeApiJmp = core.Registers["eip"];
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
				// If EIP was not changed by the stub (e.g., _apiTable stub), pop the
				// return address from the stack (it was pushed by the CALL before JMP thunk).
				// Also ensure EAX is set from TryCall result for _apiTable raw lambdas that
				// return a value but don't set EAX via calling convention machinery.
				if ( core.Registers["eip"] == eipBeforeApiJmp )
				{
					uint retAddr = core.ReadDword( core.Registers["esp"] );
					core.Registers["esp"] += 4;
					core.Registers["eip"] = retAddr;
					// Detect thiscall/thunk pattern: if the callee is a thiscall export (e.g., MFC),
					// the wrapper expects the callee to clean its args.
					// Check if this API is a known thiscall export OR if its source DLL is MFC42u.
					bool isThiscall = false;
					if ( _interpreter.ImportSourceDlls.TryGetValue( api.Key, out var srcDll ) )
						isThiscall = srcDll == "MFC42U.DLL" || _interpreter.ThiscallExports.Contains( api.Key );
					if ( isThiscall )
					{
						try
						{
							byte b0 = core.ReadByte( retAddr );
							byte b1 = core.ReadByte( retAddr + 1 );
							byte b2 = core.ReadByte( retAddr + 2 );
							byte b3 = core.ReadByte( retAddr + 3 );
							if ( b0 == 0x5D && b1 == 0xC2 ) // POP EBP; RET N -> wrapper arg count
							{
								uint retN = (uint)b2 | ((uint)b3 << 8);
								core.Registers["esp"] += retN;
							}
							else if ( b0 == 0xC2 ) // RET N
							{
								uint retN = (uint)b1 | ((uint)b2 << 8);
								core.Registers["esp"] += retN;
							}
						}
						catch { /* ignore read errors */ }
					}
				}
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
				string regName = X86AddressingHelper.GetRegisterName( rm );
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
		else if ( reg == 3 ) // CALLF r/m16:32 (far call) — treat as near call in protected flat mode
		{
			// Far calls in flat 32-bit mode are unusual; treat the target as a near address
			if ( mod == 3 )
			{
				string regName = X86AddressingHelper.GetRegisterName( rm );
				uint target = core.Registers[regName];
				core.Registers["eip"] += 2;
				core.Push( core.Registers["eip"] );
				core.Registers["eip"] = target;
			}
			else
			{
				uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				// Far pointer: 6 bytes (32-bit offset + 16-bit selector). Read only the offset.
				uint target = core.ReadDword( effectiveAddress );
				uint length = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
				core.Registers["eip"] += length;
				core.Push( core.Registers["eip"] );
				core.Registers["eip"] = target;
			}
		}
		else if ( reg == 5 ) // JMPF r/m16:32 (far jump) — treat as near jump
		{
			if ( mod == 3 )
			{
				string regName = X86AddressingHelper.GetRegisterName( rm );
				core.Registers["eip"] = core.Registers[regName];
			}
			else
			{
				uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
				uint target = core.ReadDword( effectiveAddress );
				core.Registers["eip"] = target;
			}
		}
		else if ( reg == 7 ) // Technically undefined — treat as NOP
		{
			Log.Warning( $"0xFF /7 (modrm=0x{modrm:X2}) at 0x{eip:X8} — treating as NOP" );
			if ( mod == 3 ) core.Registers["eip"] += 2;
			else core.Registers["eip"] += X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		}
		else
		{
			throw new InvalidOperationException( $"Unimplemented 0xFF /{reg} (modrm=0x{modrm:X2}, mod={mod}, rm={rm})" );
		}
	}


}
