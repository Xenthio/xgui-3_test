using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

[TestClass]
public class XPMspaintTrace
{
    private const string XPProgDir = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\xpprog\";

    [TestMethod]
    public void XPMspaint_StepTrace()
    {
        string exePath = XPProgDir + "mspaint.exe";
        if (!File.Exists(exePath)) Assert.Inconclusive("mspaint.exe not found");

        var interp = new X86Interpreter();
        interp.LoadExecutable(File.ReadAllBytes(exePath), exePath);
        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;

        int steps = 0;
        const int MAX = 200;

        while (steps < MAX)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE) { Console.WriteLine($"Exit sentinel at step {steps}"); break; }

            uint ebx = core.Registers["ebx"];
            uint esp = core.Registers["esp"];
            uint eax = core.Registers["eax"];
            uint ecx = core.Registers["ecx"];
            uint esi = core.Registers["esi"];
            byte op1 = 0, op2 = 0, op3 = 0, op4 = 0, op5 = 0;
            try { op1 = core.ReadByte(eip); op2 = core.ReadByte(eip+1); op3 = core.ReadByte(eip+2); op4 = core.ReadByte(eip+3); op5 = core.ReadByte(eip+4); } catch { }

            if (steps >= 78 && steps <= 95)
                Console.WriteLine($"Step {steps}: EIP=0x{eip:X8} EBX=0x{ebx:X8} EAX=0x{eax:X8} ECX=0x{ecx:X8} ESP=0x{esp:X8} ESI=0x{esi:X8} op={op1:X2} {op2:X2} {op3:X2} {op4:X2} {op5:X2}");

            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                Console.WriteLine($"FAULT at step {steps}: {real.Message}");
                break;
            }
            steps++;
        }
        Console.WriteLine($"Done: {steps} steps");
    }
}
