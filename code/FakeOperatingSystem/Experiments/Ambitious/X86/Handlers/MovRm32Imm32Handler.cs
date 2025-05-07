using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovRm32Imm32Handler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0xC7;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte modrm = core.ReadByte(eip + 1);
        byte mod = (byte)(modrm >> 6);
        byte rm = (byte)(modrm & 0x7);
        
        // Read the immediate value
        uint imm32;
        
        if (mod == 3) // Register destination
        {
            string destReg = GetRegisterName(rm);
            imm32 = core.ReadDword(eip + 2);
            core.Registers[destReg] = imm32;
            core.Registers["eip"] += 6; // opcode + modrm + imm32
        }
        else // Memory destination
        {
            uint effectiveAddress = CalculateEffectiveAddress(core, modrm, eip);
            uint offset = GetModRMSize(mod, rm);
            imm32 = core.ReadDword(eip + 1 + offset);
            
            // Write the immediate value to memory
            core.WriteDword(effectiveAddress, imm32);
            
            // Advance EIP: opcode + modrm + possible SIB/disp + imm32
            core.Registers["eip"] += 1 + offset + 4;
        }
    }
    
    private uint CalculateEffectiveAddress(X86Core core, byte modrm, uint eip)
    {
        byte mod = (byte)(modrm >> 6);
        byte rm = (byte)(modrm & 0x7);
        
        if (mod == 0 && rm == 5) // [disp32]
            return core.ReadDword(eip + 2);
            
        uint ea = 0;
        
        // Base register
        if (rm != 4) // Not SIB
            ea = core.Registers[GetRegisterName(rm)];
        else
            throw new NotImplementedException("SIB addressing not implemented");
            
        // Displacement
        if (mod == 1) // 8-bit displacement
            ea += (uint)(sbyte)core.ReadByte(eip + 2);
        else if (mod == 2) // 32-bit displacement
            ea += core.ReadDword(eip + 2);
            
        return ea;
    }
    
    private uint GetModRMSize(byte mod, byte rm)
    {
        if (mod == 0 && rm == 5) // [disp32]
            return 5;
        else if (mod == 0) // [reg]
            return 1;
        else if (mod == 1) // [reg+disp8]
            return 2;
        else if (mod == 2) // [reg+disp32]
            return 5;
        else // mod == 3, register to register
            return 1;
    }
    
    private string GetRegisterName(int code) => code switch
    {
        0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
        4 => "esp", 5 => "ebp", 6 => "esi", 7 => "edi",
        _ => throw new ArgumentException($"Invalid register code: {code}")
    };
}

