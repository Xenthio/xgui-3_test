using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovRegImm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode >= 0xB8 && opcode <= 0xBF;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );
		int reg = opcode - 0xB8;
		uint imm = core.ReadDword( eip + 1 );
		string regName = GetRegisterName( reg );
		core.Registers[regName] = imm;
		core.Registers["eip"] += 5;
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
