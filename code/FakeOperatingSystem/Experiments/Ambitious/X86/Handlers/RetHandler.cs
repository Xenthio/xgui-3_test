using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class RetHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0xC3 || opcode == 0xC2;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);
        
        if (opcode == 0xC3) // RET
        {
            core.Registers["eip"] = core.Pop();
        }
        else // RET imm16
        {
            ushort imm16 = (ushort)(core.ReadByte(eip + 1) | (core.ReadByte(eip + 2) << 8));
            core.Registers["eip"] = core.Pop();
            core.Registers["esp"] += imm16;
        }
    }
}
