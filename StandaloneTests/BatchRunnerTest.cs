// Batch runner: tries several NT4 programs and asserts no faults
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

[TestClass]
public class BatchRunnerTests
{
    private const string NtProgDir =
        @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt4prog\";

    private static (int steps, bool faulted, string faultMsg, string classes) RunExe(string exePath, int maxSteps = 100_000)
    {
        if (!File.Exists(exePath)) return (-1, false, "not found", "");

        var interp = new X86Interpreter();
        interp.LoadExecutable(File.ReadAllBytes(exePath), exePath);
        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;
        var user32 = interp.APIEmulators.OfType<User32Emulator>().First();

        int steps = 0;
        bool faulted = false;
        string faultMsg = null;
        bool quitSent = false;

        while (steps < maxSteps)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE) break;
            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                // Unwrap TargetInvocationException to get the real inner error
                var realEx = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                string msg = realEx.Message;
                if (msg.Contains("Invalid return address") || msg.Contains("Invalid Return Address"))
                { Console.WriteLine($"[Normal exit at EIP=0x{eip:X8}] {msg}"); break; }
                faulted = true;
                faultMsg = $"EIP=0x{eip:X8}: {realEx.GetType().Name}: {msg}";
                break;
            }
            steps++;
            if (!quitSent && steps > 50_000)
            {
                if (user32.WndProcByHwnd.Count > 0)
                    user32.PostWinMsg(user32.WndProcByHwnd.Keys.First(), 0x000F, 0, 0);
                user32.PostWinMsg(0, 0x0012, 0, 0);
                quitSent = true;
            }
        }

        string classes = string.Join(", ", user32.WndProcByClass.Select(kv => $"'{kv.Key}'=0x{kv.Value:X8}"));
        Console.WriteLine($"{Path.GetFileName(exePath)}: steps={steps} classes=[{classes}] windows={user32.WndProcByHwnd.Count} faulted={faulted} {faultMsg}");
        return (steps, faulted, faultMsg, classes);
    }

    [TestMethod] public void CdPlayer_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "cdplayer.exe");
        if (steps < 0) Assert.Inconclusive("cdplayer.exe not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    [TestMethod] public void SndVol32_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "sndvol32.exe");
        if (steps < 0) Assert.Inconclusive("sndvol32.exe not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    [TestMethod] public void SndRec32_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "sndrec32.exe");
        if (steps < 0) Assert.Inconclusive("sndrec32.exe not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
}
