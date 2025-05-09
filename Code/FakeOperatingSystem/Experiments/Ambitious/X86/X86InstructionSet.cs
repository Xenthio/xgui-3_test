﻿using FakeDesktop;
using System.Collections.Generic;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public class X86InstructionSet
{
	private readonly List<IInstructionHandler> _handlers = new();

	public void RegisterHandler( IInstructionHandler handler ) => _handlers.Add( handler );

	public void ExecuteNext( X86Core core, X86Interpreter interpreter = null )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		foreach ( var handler in _handlers )
		{
			if ( handler.CanHandle( opcode ) )
			{
				handler.Execute( core );
				return;
			}
		}

		// No handler found - better error handling
		if ( interpreter != null )
		{
			interpreter.HaltWithMessageBox(
				"Illegal Instruction",
				$"The program attempted to execute an unimplemented or illegal opcode: 0x{opcode:X2} at 0x{eip:X8}\n\n" +
				$"Press Abort to terminate execution, Retry to attempt continuing, or Ignore to skip this instruction.",
				MessageBoxIcon.Error
			);
		}
		else
		{
			throw new System.InvalidOperationException( $"Unknown opcode: 0x{opcode:X2} at 0x{eip:X8}" );
		}
	}
}
