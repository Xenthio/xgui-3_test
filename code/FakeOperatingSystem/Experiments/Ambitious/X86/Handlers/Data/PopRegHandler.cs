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
        string regName = GetRegisterName(regCode);
        
        // Pop from stack to register
        core.Registers[regName] = core.Pop();
        
        // Advance EIP
        core.Registers["eip"] += 1;
    }
    
    private string GetRegisterName(int code) => code switch
    {
        0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
        4 => "esp", 5 => "ebp", 6 => "esi", 7 => "edi",
        _ => throw new ArgumentException($"Invalid register code: {code}")
    };
}
