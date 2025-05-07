using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class CallRel32Handler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0xE8;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        int relOffset = (int)core.ReadDword(eip + 1);
        
        // Push the return address
        core.Push(eip + 5);
        
        // Calculate the target address
        core.Registers["eip"] = (uint)((int)eip + 5 + relOffset);
    }
}
