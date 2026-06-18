using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

[TestClass]
public class MspaintTraceTest
{
    private const string ExePath = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt4prog\mspaint.exe";

    [TestMethod]
    public void Mspaint_Trace500()
    {
        if (!File.Exists(ExePath)) Assert.Inconclusive("not found");

        var interp = new X86Interpreter();
        interp.LoadExecutable(File.ReadAllBytes(ExePath), ExePath);
        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;

        int steps = 0;
        bool faulted = false;
        string faultMsg = null;

        while (steps < 500)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE) { Console.WriteLine($"STOPPED at step {steps} eip=0x{eip:X8}"); break; }

            string op = "";
            try
            {
                var raw = new byte[6];
                for (int i = 0; i < 6; i++) try { raw[i] = core.ReadByte((uint)(eip+i)); } catch {}
                op = string.Join(" ", raw.Select(b => b.ToString("x2")));
            }
            catch {}

            Console.WriteLine($"  step {steps,4}  EIP=0x{eip:X8}  [{op}]  EAX=0x{core.Registers["eax"]:X8}  ESP=0x{core.Registers["esp"]:X8}");

            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                if (real.Message.Contains("Invalid return address")) { Console.WriteLine($"[Exit] {real.Message}"); break; }
                faulted = true; faultMsg = $"EIP=0x{eip:X8}: {real.GetType().Name}: {real.Message}";
                break;
            }
            steps++;
        }

        Assert.IsFalse(faulted, faultMsg);
    }
}
