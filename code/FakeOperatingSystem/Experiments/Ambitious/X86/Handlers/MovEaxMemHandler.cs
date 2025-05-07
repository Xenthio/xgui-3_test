using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovEaxMemHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0xA1;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        
        // This instruction has a direct memory operand - the 32-bit address follows
        // the opcode directly in the instruction stream
        uint address = core.ReadDword(eip + 1);
        
        // Read the dword from that address
        uint value = core.ReadDword(address);
        
        // Store in EAX
        core.Registers["eax"] = value;
        
        // Advance EIP past opcode and operand (5 bytes total)
        core.Registers["eip"] += 5;
    }
}

