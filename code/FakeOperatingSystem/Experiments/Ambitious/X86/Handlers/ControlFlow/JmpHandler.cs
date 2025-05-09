using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class JmpHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0xE9 || opcode == 0xEB;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);
        
        if (opcode == 0xE9) // JMP rel32
        {
            int rel32 = (int)core.ReadDword(eip + 1);
            core.Registers["eip"] = (uint)((int)eip + 5 + rel32);
        }
        else // JMP rel8
        {
            sbyte rel8 = (sbyte)core.ReadByte(eip + 1);
            core.Registers["eip"] = (uint)((int)eip + 2 + rel8);
        }
    }
}
