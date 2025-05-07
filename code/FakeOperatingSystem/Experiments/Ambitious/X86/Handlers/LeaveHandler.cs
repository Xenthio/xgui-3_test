using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class LeaveHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0xC9;

    public void Execute(X86Core core)
    {
        core.Registers["esp"] = core.Registers["ebp"];
        core.Registers["ebp"] = core.Pop();
        core.Registers["eip"] += 1;
    }
}
