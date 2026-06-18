using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class NopHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) =>
        opcode == 0x90 || // NOP
        opcode == 0xCC || // INT3 (software breakpoint) — treat as NOP in emulation
        opcode == 0xF0 || // LOCK prefix — no-op (we don’t emulate multi-core atomics)
        opcode == 0x63;   // ARPL (32-bit) / MOVSXD (64-bit) — treat as NOP in 32-bit mode

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);
        if (opcode == 0xCC)
            core.LogVerbose($"INT3 at 0x{eip:X8} (treated as NOP)");
        else if (opcode == 0xF0)
            core.LogVerbose($"LOCK prefix at 0x{eip:X8} (ignored)");
        else if (opcode == 0x63)
            core.LogVerbose($"ARPL/MOVSXD at 0x{eip:X8} (treated as NOP)");
        // Advance EIP past this 1-byte instruction/prefix
        core.Registers["eip"] += 1;
    }
}
