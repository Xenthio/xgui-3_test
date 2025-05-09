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
        string regName = GetRegisterName(regCode);
        
        // Push register value onto stack
        core.Push(core.Registers[regName]);
        
        // Advance EIP
        core.Registers["eip"] += 1;
    }
    
    private string GetRegisterName(int code) => code switch
    {
        0 => "eax",
        1 => "ecx",
        2 => "edx",
        3 => "ebx",
        4 => "esp",
        5 => "ebp",
        6 => "esi",
        7 => "edi",
        _ => throw new ArgumentException($"Invalid register code: {code}")
    };
}
