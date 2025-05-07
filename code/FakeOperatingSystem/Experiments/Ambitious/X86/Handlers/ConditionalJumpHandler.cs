using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class ConditionalJumpHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => (opcode >= 0x70 && opcode <= 0x7F) || (opcode >= 0x0F80 && opcode <= 0x0F8F);

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);
        bool condition = false;
        
        // Short jumps (0x70-0x7F)
        if (opcode >= 0x70 && opcode <= 0x7F)
        {
            sbyte offset = (sbyte)core.ReadByte(eip + 1);
            condition = EvaluateCondition(opcode - 0x70, core);
            
            if (condition)
                core.Registers["eip"] = (uint)((int)eip + 2 + offset);
            else
                core.Registers["eip"] += 2;
        }
        else
        {
            // TODO: Handle near conditional jumps (0x0F 0x80-0x8F)
            core.Registers["eip"] += 2;
        }
    }
    
    private bool EvaluateCondition(int condCode, X86Core core)
    {
        switch (condCode)
        {
            case 0x4: return core.ZeroFlag;                  // JE/JZ
            case 0x5: return !core.ZeroFlag;                 // JNE/JNZ
            case 0x2: return core.CarryFlag;                 // JB/JNAE/JC
            case 0x3: return !core.CarryFlag;                // JAE/JNB/JNC
            case 0x6: return core.ZeroFlag || core.CarryFlag; // JBE/JNA
            case 0x7: return !core.ZeroFlag && !core.CarryFlag; // JA/JNBE
            case 0x8: return core.SignFlag;                  // JS
            case 0x9: return !core.SignFlag;                 // JNS
            case 0xC: return core.SignFlag != core.OverflowFlag; // JL/JNGE
            case 0xD: return core.SignFlag == core.OverflowFlag; // JGE/JNL
            case 0xE: return core.ZeroFlag || (core.SignFlag != core.OverflowFlag); // JLE/JNG
            case 0xF: return !core.ZeroFlag && (core.SignFlag == core.OverflowFlag); // JG/JNLE
            default: return false;
        }
    }
}
