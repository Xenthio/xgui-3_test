using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class XchgHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => 
        (opcode >= 0x90 && opcode <= 0x97) || // XCHG EAX, r32
        opcode == 0x86 ||                     // XCHG r/m8, r8
        opcode == 0x87;                       // XCHG r/m32, r32

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);
        
        if (opcode >= 0x90 && opcode <= 0x97)
        {
            // XCHG EAX, r32 (single-byte encoding)
            
            if (opcode == 0x90) // NOP (XCHG EAX, EAX)
            {
                core.Registers["eip"]++;
                return;
            }
            
            // Get the target register
            string regName = GetRegisterFromOpcode(opcode);
            
            // Swap register values
            uint temp = core.Registers["eax"];
            core.Registers["eax"] = core.Registers[regName];
            core.Registers[regName] = temp;
            
            Log.Info($"XCHG EAX, {regName.ToUpper()}: Swapped EAX={core.Registers["eax"]:X8} and {regName.ToUpper()}={core.Registers[regName]:X8}");
            
            core.Registers["eip"]++;
        }
        else
        {
            // XCHG r/m8, r8 (0x86) or XCHG r/m32, r32 (0x87)
            byte modrm = core.ReadByte(eip + 1);
            byte mod = (byte)(modrm >> 6);
            byte reg = (byte)((modrm >> 3) & 0x7);
            byte rm = (byte)(modrm & 0x7);
            
            string regName = X86AddressingHelper.GetRegisterName(reg);
            
            if (mod == 3) // Register to register
            {
                string rmRegName = X86AddressingHelper.GetRegisterName(rm);
                
                // Swap register values
                uint temp = core.Registers[regName];
                core.Registers[regName] = core.Registers[rmRegName];
                core.Registers[rmRegName] = temp;
                
                Log.Info($"XCHG {regName.ToUpper()}, {rmRegName.ToUpper()}: Swapped {regName.ToUpper()}={core.Registers[regName]:X8} and {rmRegName.ToUpper()}={core.Registers[rmRegName]:X8}");
                
                core.Registers["eip"] += 2;
            }
            else // Memory operand
            {
                // Calculate effective address
                uint effectiveAddress = X86AddressingHelper.CalculateEffectiveAddress(core, modrm, eip);
                
                if (opcode == 0x86) // XCHG r/m8, r8
                {
                    // Get register value (low byte)
                    byte regValue = (byte)(core.Registers[regName] & 0xFF);
                    
                    // Get memory value
                    byte memValue = core.ReadByte(effectiveAddress);
                    
                    // Swap values
                    core.WriteByte(effectiveAddress, regValue);
                    core.Registers[regName] = (core.Registers[regName] & 0xFFFFFF00) | memValue;
                }
                else // XCHG r/m32, r32
                {
                    // Get register value
                    uint regValue = core.Registers[regName];
                    
                    // Get memory value
                    uint memValue = core.ReadDword(effectiveAddress);
                    
                    // Swap values
                    core.WriteDword(effectiveAddress, regValue);
                    core.Registers[regName] = memValue;
                }
                
                uint length = X86AddressingHelper.GetInstructionLength(modrm);
                core.Registers["eip"] += length;
            }
        }
    }
    
    private string GetRegisterFromOpcode(byte opcode)
    {
        return (opcode - 0x90) switch
        {
            0 => "eax", // 0x90
            1 => "ecx", // 0x91
            2 => "edx", // 0x92
            3 => "ebx", // 0x93
            4 => "esp", // 0x94
            5 => "ebp", // 0x95
            6 => "esi", // 0x96
            7 => "edi", // 0x97
            _ => throw new ArgumentException($"Invalid register opcode: {opcode}")
        };
    }
}

