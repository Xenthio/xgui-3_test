namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// PUSHA (0x60) — push EAX, ECX, EDX, EBX, original ESP, EBP, ESI, EDI (in that order).
/// POPA  (0x61) — pop  EDI, ESI, EBP, (skip ESP), EBX, EDX, ECX, EAX (in that order).
/// These are deprecated in 64-bit mode but common in legacy 32-bit code.
/// </summary>
public class PushaPopsHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0x60 || opcode == 0x61;

    private static readonly string[] RegOrder = { "eax", "ecx", "edx", "ebx", "esp", "ebp", "esi", "edi" };

    public void Execute(X86Core core)
    {
        uint eip = core.Registers["eip"];
        byte opcode = core.ReadByte(eip);

        if (opcode == 0x60) // PUSHA — push all 8 general-purpose registers
        {
            uint savedEsp = core.Registers["esp"];
            foreach (var reg in RegOrder)
            {
                // PUSHA pushes the original ESP (before any pushes), not the modified one
                uint value = reg == "esp" ? savedEsp : core.Registers[reg];
                core.Push(value);
            }
        }
        else // 0x61 — POPA — pop into EDI, ESI, EBP, (discard for ESP), EBX, EDX, ECX, EAX
        {
            // Pop order is reverse of push: EDI first, EAX last
            // But ESP is skipped (add 4 to discard) rather than loaded
            for (int i = RegOrder.Length - 1; i >= 0; i--)
            {
                string reg = RegOrder[i];
                uint value = core.Pop();
                if (reg != "esp") // don't restore ESP from stack
                    core.Registers[reg] = value;
            }
        }

        core.Registers["eip"] += 1;
    }
}
