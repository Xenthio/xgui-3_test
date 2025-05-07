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

		if ( reg == 2 ) // CALL r/m32
		{
			uint target;
			if ( mod == 3 )
			{
				string regName = GetRegisterName( rm );
				target = core.Registers[regName];
				core.Registers["eip"] += 2;
			}
			else if ( mod == 0 && rm == 5 )
			{
				uint addr = core.ReadDword( eip + 2 );
				target = core.ReadDword( addr );
				core.Registers["eip"] += 6;
			}
			else
			{
				throw new InvalidOperationException( $"Unimplemented CALL [mod={mod}, rm={rm}]" );
			}

			Log.Info( $"OpcodeFFHandler: _imports = [{string.Join( ", ", _interpreter.Imports.Select( x => $"{x.Key}={x.Value:X8}" ) )}]" );
			Log.Info( $"OpcodeFFHandler: Looking for target=0x{target:X8}" );
			var api = _interpreter.Imports.FirstOrDefault( x => x.Value == target );
			Log.Info( $"OpcodeFFHandler: api.Key = {api.Key}" );

			Log.Info( $"OpcodeFFHandler: CALL target=0x{target:X8}" );

			// Push return address
			core.Push( core.Registers["eip"] );

			if ( api.Key != null )
			{
				Log.Info( $"OpcodeFFHandler: Detected API call to {api.Key}" );
				foreach ( var emu in _interpreter.APIEmulators )
				{
					if ( emu.TryCall( api.Key, core, out var result ) )
					{
						core.Registers["eax"] = result;
						core.Registers["eip"] = core.Pop(); // Return from API call
						return;
					}
				}
			}

			// Normal call
			core.Registers["eip"] = target;
		}
		else
		{
			throw new InvalidOperationException( $"Unimplemented 0xFF /{reg}" );
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
