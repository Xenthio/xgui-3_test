using System.Collections.Generic;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public class X86InstructionSet
{
	private readonly List<IInstructionHandler> _handlers = new();

	public void RegisterHandler( IInstructionHandler handler ) => _handlers.Add( handler );

	public void ExecuteNext( X86Core core )
	{
		byte opcode = core.ReadByte( core.Registers["eip"] );
		foreach ( var handler in _handlers )
		{
			if ( handler.CanHandle( opcode ) )
			{
				handler.Execute( core );
				return;
			}
		}
		throw new System.InvalidOperationException( $"Unknown opcode: 0x{opcode:X2}" );
	}
}
