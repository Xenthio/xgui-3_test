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
		string regName = X86AddressingHelper.GetRegisterName( reg );
		core.LogVerbose( $"MOV {regName}, 0x{imm:X8} at EIP=0x{eip:X8}" );
		core.Registers[regName] = imm;
		core.Registers["eip"] += 5;
	}


}
