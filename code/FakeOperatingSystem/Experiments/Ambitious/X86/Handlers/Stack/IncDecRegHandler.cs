using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class IncDecRegHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => (opcode >= 0x40 && opcode <= 0x47) || (opcode >= 0x48 && opcode <= 0x4F);

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);
        
        if (opcode >= 0x40 && opcode <= 0x47) // INC
        {
            string reg = GetRegisterName(opcode - 0x40);
            uint result = core.Registers[reg] + 1;
            core.Registers[reg] = result;
            
            core.ZeroFlag = result == 0;
            core.SignFlag = (result & 0x80000000) != 0;
            // Carry flag is not affected by INC
            core.OverflowFlag = result == 0x80000000; // Only overflows if incrementing from 0x7FFFFFFF to 0x80000000
        }
        else // DEC
        {
            string reg = GetRegisterName(opcode - 0x48);
            uint result = core.Registers[reg] - 1;
            core.Registers[reg] = result;
            
            core.ZeroFlag = result == 0;
            core.SignFlag = (result & 0x80000000) != 0;
            // Carry flag is not affected by DEC
            core.OverflowFlag = result == 0x7FFFFFFF; // Only overflows if decrementing from 0x80000000 to 0x7FFFFFFF
        }
        
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
