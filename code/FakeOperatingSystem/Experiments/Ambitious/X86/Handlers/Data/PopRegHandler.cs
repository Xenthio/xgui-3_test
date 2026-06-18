using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class PopRegHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode >= 0x58 && opcode <= 0x5F;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);
        
        int regCode = opcode - 0x58;
        string regName = X86AddressingHelper.GetRegisterName(regCode);
        
        // Pop from stack to register
        core.Registers[regName] = core.Pop();
        
        // Advance EIP
        core.Registers["eip"] += 1;
    }
    

}
