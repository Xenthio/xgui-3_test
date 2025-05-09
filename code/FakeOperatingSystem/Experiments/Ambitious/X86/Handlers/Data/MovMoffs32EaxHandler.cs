using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class MovMoffs32EaxHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0xA3;

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        // Read the 32-bit immediate address
        uint addr = core.ReadDword(eip + 1);
        uint value = core.Registers["eax"];
        core.WriteDword(addr, value);
        core.Registers["eip"] += 5; // 1 (opcode) + 4 (address)
        core.LogVerbose($"MOV [0x{addr:X8}], EAX (0x{value:X8})");
    }
}
