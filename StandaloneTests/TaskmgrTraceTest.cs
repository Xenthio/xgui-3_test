using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

[TestClass]
public class TaskmgrTraceTest
{
    private const string XPProgDir = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\xpprog\";

    [TestMethod]
    public void TaskmgrStepTrace()
    {
        string exePath = XPProgDir + "taskmgr.exe";
        Assert.IsTrue(File.Exists(exePath), "taskmgr.exe not found");

        var interp = new X86Interpreter();
        interp.LoadExecutable(File.ReadAllBytes(exePath), exePath);
        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;

        int steps = 0;
        int maxSteps = 300;

        while (steps < maxSteps)
        {
            uint eip = core.Registers["eip"];
            uint esp = core.Registers["esp"];
            uint ecx = core.Registers["ecx"];
            uint eax = core.Registers["eax"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE) break;
            
            // Log every step for the trace
            byte op1 = 0, op2 = 0;
            try { op1 = core.ReadByte(eip); op2 = core.ReadByte(eip+1); } catch { }
            
            if (steps > 240) // Show last steps before crash
                Console.WriteLine($"Step {steps}: EIP=0x{eip:X8} ESP=0x{esp:X8} ECX=0x{ecx:X8} EAX=0x{eax:X8} op=0x{op1:X2} 0x{op2:X2}");

            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                Console.WriteLine($"FAULT at step {steps}: EIP=0x{eip:X8} ECX=0x{ecx:X8}: {real.Message}");
                break;
            }
            steps++;
        }
        Console.WriteLine($"Done: {steps} steps");
    }
}
