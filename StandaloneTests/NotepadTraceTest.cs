using System;
using System.IO;
using System.Linq;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace X86StandaloneTests;

[TestClass]
public class NotepadTraceTest
{
    [TestMethod]
    public void Notepad_Trace210()
    {
        string exePath = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt4prog\notepad.exe";
        if (!File.Exists(exePath)) Assert.Inconclusive("not found");

        var interp = new X86Interpreter();
        interp.LoadExecutable(File.ReadAllBytes(exePath), exePath);
        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;

        int steps = 0;
        while (steps < 1400)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE)
            {
                Console.WriteLine($"  [STOPPED at step {steps}, EIP=0x{eip:X8}]");
                break;
            }
            byte op = 0;
            try { op = core.ReadByte(eip); } catch { }
            Console.WriteLine($"  step {steps,3} EIP=0x{eip:X8} op=0x{op:X2}  ESP=0x{core.Registers["esp"]:X8}  EAX=0x{core.Registers["eax"]:X8}  ECX=0x{core.Registers["ecx"]:X8}");
            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                Console.WriteLine($"  FAULT step {steps}: {real.GetType().Name}: {real.Message}");
                break;
            }
            steps++;
        }
        Console.WriteLine($"Final: steps={steps} EIP=0x{core.Registers["eip"]:X8}");
    }
}
