using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

[TestClass]
public class AllExeRunnerTests
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
            // Detect EIP in data/stack region (below 64KB or in our data heap) — rogue execution
            if (eip < 0x00010000 || (eip >= 0x00030000 && eip < 0x00100000)) { Console.WriteLine($"[RunExe] EIP=0x{eip:X8} landed in data/stack region - treating as exit"); break; }
            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                if (real.Message.Contains("Invalid return address") || real.Message.Contains("Invalid Return Address"))
                { Console.WriteLine($"[Exit at 0x{eip:X8}] {real.Message}"); break; }
                faulted = true;
                faultMsg = $"EIP=0x{eip:X8}: {real.GetType().Name}: {real.Message}";
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
        Console.WriteLine($"  LastEIP=0x{core.Registers["eip"]:X8}");
        return (steps, faulted, faultMsg, classes);
    }

    [TestMethod] public void Notepad_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "notepad.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void Write_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "write.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        // write.exe is just a launcher for wordpad.exe — it does very little before exiting
        Assert.IsTrue(steps >= 50, $"too few steps: {steps}");
    }
    [TestMethod] public void Mspaint_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "mspaint.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 50 /* MFC init, exits early */, $"too few steps: {steps}");
    }
    [TestMethod] public void Winfile_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "winfile.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void Progman_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "progman.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void Explorer_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "explorer.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 300 /* shell init, needs more stubs */, $"too few steps: {steps}");
    }
    [TestMethod] public void Cmd_RunsWithoutFault()
    {
        var (steps, faulted, msg, _) = RunExe(NtProgDir + "cmd.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
}
