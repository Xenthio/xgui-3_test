using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class CdqHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0x99;

    public void Execute(X86Core core)
    {
        // Get the value in EAX
        uint eax = core.Registers["eax"];
        
        // Check if the sign bit (bit 31) is set
        bool isNegative = (eax & 0x80000000) != 0;
        
        // Set EDX to either all 1s (if EAX is negative) or all 0s (if EAX is positive)
        core.Registers["edx"] = isNegative ? 0xFFFFFFFF : 0;
        
        // Advance EIP (this instruction is just 1 byte)
        core.Registers["eip"] += 1;
    }
}
