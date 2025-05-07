using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class LeaHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0x8D;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte modrm = core.ReadByte(eip + 1);
        byte mod = (byte)(modrm >> 6);
        byte reg = (byte)((modrm >> 3) & 0x7);
        byte rm = (byte)(modrm & 0x7);
        
        // LEA calculates address but doesn't dereference it
        string destReg = GetRegisterName(reg);
        
        if (mod == 1 && rm == 5) // [ebp+disp8]
        {
            sbyte disp8 = (sbyte)core.ReadByte(eip + 2);
            uint addr = core.Registers["ebp"] + (uint)disp8;
            core.Registers[destReg] = addr;
            core.Registers["eip"] += 3;
        }
        else if (mod == 2 && rm == 5) // [ebp+disp32]
        {
            uint disp32 = core.ReadDword(eip + 2);
            uint addr = core.Registers["ebp"] + disp32;
            core.Registers[destReg] = addr;
            core.Registers["eip"] += 6;
        }
        else
        {
            throw new NotImplementedException($"Unimplemented LEA addressing mode: mod={mod}, rm={rm}");
        }
    }
    
    private string GetRegisterName(int code) => code switch
    {
        0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
        4 => "esp", 5 => "ebp", 6 => "esi", 7 => "edi",
        _ => throw new ArgumentException($"Invalid register code: {code}")
    };
}
