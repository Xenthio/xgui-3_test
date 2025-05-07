using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class NopHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0x90;

    public void Execute(X86Core core)
    {
        // NOP does nothing, just advances EIP
        core.Registers["eip"] += 1;
    }
}
