using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class PushRegHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode >= 0x50 && opcode <= 0x57;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);
        
        // Register is encoded in the low 3 bits of the opcode
        int regCode = opcode - 0x50;
        string regName = X86AddressingHelper.GetRegisterName(regCode);
        
        // Push register value onto stack
        core.Push(core.Registers[regName]);
        
        // Advance EIP
        core.Registers["eip"] += 1;
    }
    

}
