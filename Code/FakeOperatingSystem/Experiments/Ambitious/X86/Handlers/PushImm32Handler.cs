using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class PushImm32Handler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0x68;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        uint imm32 = core.ReadDword(eip + 1);
        core.Push(imm32);
        core.Registers["eip"] += 5;
    }
}
