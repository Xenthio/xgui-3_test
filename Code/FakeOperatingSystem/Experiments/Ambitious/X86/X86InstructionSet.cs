using FakeDesktop;
using Sandbox;
using System.Collections.Generic;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public class X86InstructionSet
{
	[ConVar( "xguitest_x86_log_opcode" )]
	public static bool OpcodeLogging { get; set; } = false;
	private readonly List<IInstructionHandler> _handlers = new();

	public void RegisterHandler( IInstructionHandler handler ) => _handlers.Add( handler );

	public void ExecuteNext( X86Core core, X86Interpreter interpreter = null )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		if ( OpcodeLogging )
		{
			Log.Info( $"EIP=0x{eip:X8}: Executing opcode 0x{opcode:X2} (ECX={core.Registers["ecx"]:X8})" );
		}

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
			var result = interpreter.HaltWithMessageBoxAsync(
				"Illegal Instruction",
				$"The program attempted to execute an unimplemented or illegal opcode: 0x{opcode:X2} at 0x{eip:X8}\n\n" +
				$"Press Abort to terminate execution, Retry to attempt continuing, or Ignore to skip this instruction.",
				MessageBoxIcon.Error,
				MessageBoxButtons.AbortRetryIgnore
			);
			if ( result != null )
			{
				switch ( result.Result )
				{
					case MessageBoxResult.Abort:
						throw new System.InvalidOperationException( $"!The program attempted to execute an unimplemented or illegal opcode: 0x{opcode:X2} at 0x{eip:X8}" );
					case MessageBoxResult.Retry:
						Log.Info( $"Retrying execution of opcode 0x{opcode:X2} at 0x{eip:X8}" );
						break;
					case MessageBoxResult.Ignore:
						Log.Warning( $"Ignoring illegal opcode 0x{opcode:X2} at 0x{eip:X8}" );
						core.Registers["eip"]++;
						return;
				}
			}
		}
		else
		{
			throw new System.InvalidOperationException( $"Unknown opcode: 0x{opcode:X2} at 0x{eip:X8}" );
		}
	}
}
