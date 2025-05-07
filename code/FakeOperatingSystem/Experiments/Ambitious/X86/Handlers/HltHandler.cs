using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class HltHandler : IInstructionHandler
{
    public bool CanHandle(byte opcode) => opcode == 0xF4;

    public void Execute(X86Core core)
    {
        // In real hardware, HLT stops the CPU until an interrupt arrives
        // In our emulator, we can either:
        // 1. Just advance EIP (treat as NOP)
        // 2. Consider execution complete
        
        Log.Info("HLT instruction encountered - CPU execution halted");
        
        // Advance past the instruction
        core.Registers["eip"] += 1;
        
        // Optionally, if you want to stop execution when HLT is encountered:
        // throw new Exception("Program execution halted via HLT instruction");
    }
}

