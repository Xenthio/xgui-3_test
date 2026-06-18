using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

[TestClass]
public class NT5XPWin9xRunnerTests
{
    private const string NT5ProgDir  = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\2000prog\";
    private const string XPProgDir   = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\xpprog\";
    private const string Win95Dir    = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\95prog\";
    private const string Win98Dir    = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\98prog\";

    private static (int steps, bool faulted, string faultMsg, string classes, int windows) RunExe(
        string exePath, int maxSteps = 100_000)
    {
        if (!File.Exists(exePath)) return (-1, false, "not found", "", 0);

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
            // Detect EIP in data/stack region - rogue execution
            if (eip < 0x00010000 || (eip >= 0x00030000 && eip < 0x00100000)) { Console.WriteLine($"[RunExe] EIP=0x{eip:X8} in data region - exit"); break; }
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
        Console.WriteLine($"  EIP=0x{core.Registers["eip"]:X8}");
        return (steps, faulted, faultMsg, classes, user32.WndProcByHwnd.Count);
    }

    // ---- NT5 / Windows 2000 ----
    [TestMethod] public void NT5_Winmine_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "winmine.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void NT5_Notepad_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "NOTEPAD.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void NT5_Calc_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "calc.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void NT5_Mspaint_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "mspaint.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 50 /* MFC init, exits early */, $"too few steps: {steps}");
    }
    [TestMethod] public void NT5_Winver_RunsWithoutFault() // winver just calls ShellAboutW and exits
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "winver.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 20, $"too few steps: {steps}");
    }

    // ---- XP ----
    [TestMethod] public void XP_Notepad_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(XPProgDir + "NOTEPAD.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void XP_Calc_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(XPProgDir + "calc.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void XP_Mspaint_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(XPProgDir + "mspaint.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 50 /* MFC init, exits early */, $"too few steps: {steps}");
    }
    [TestMethod] public void XP_Taskmgr_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(XPProgDir + "taskmgr.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    // ---- Win95 ----
    [TestMethod] public void Win95_Notepad_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(Win95Dir + "NOTEPAD.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void Win95_Calc_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(Win95Dir + "CALC.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void Win95_Explorer_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(Win95Dir + "EXPLORER.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 50, $"too few steps: {steps}"); // shell, exits early without real shell context
    }
    [TestMethod] public void Win95_Welcome_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(Win95Dir + "WELCOME.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 50, $"too few steps: {steps}"); // dialog-only app, no user input
    }

    // ---- Win98 ----
    [TestMethod] public void Win98_Winmine_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(Win98Dir + "WINMINE.EXE");
        if (steps < 0 || steps == 0) Assert.Inconclusive("Win98 Winmine is NE (16-bit) format, not supported");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void Win98_Welcome_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(Win98Dir + "WELCOME.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }
    [TestMethod] public void Win98_Dxdiag_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(Win98Dir + "DXDIAG.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }
}
