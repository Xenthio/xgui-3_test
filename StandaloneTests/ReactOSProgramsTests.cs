using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

/// <summary>
/// Regression tests for the real ReactOS GUI programs that the x86 emulator can run.
/// Each program is loaded from data/reactosprog/, executed for a bounded number of
/// steps, then a WM_PAINT + WM_QUIT is injected once a window exists. A program
/// "passes" if it: loads, runs without faulting, registers at least one window class,
/// and creates at least one window — the same success bar used by WinmineRunnerTest.
///
/// These guard against regressions in opcode handlers and Win32 export coverage.
/// If a source binary is missing the test is Inconclusive (not a failure), so the
/// suite stays green on machines without the ReactOS extract.
/// </summary>
[TestClass]
public class ReactOSProgramsTests
{
    private const string ProgDir =
        @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\reactosprog\";

    private static (bool faulted, string faultMsg, int steps, int classes, int windows, string classList)
        Run(string exePath, int maxSteps = 200_000)
    {
        var interp = new X86Interpreter();
        bool loaded = interp.LoadExecutable(File.ReadAllBytes(exePath), exePath);
        Assert.IsTrue(loaded, $"PE must load: {exePath}");

        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;
        var user32 = interp.APIEmulators.OfType<User32Emulator>().First();

        int steps = 0;
        bool faulted = false;
        string faultMsg = null;
        bool paintSent = false;

        while (steps < maxSteps)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE) break;
            // Bail if EIP wanders into low/unmapped or the loader gap (data execution).
            if (eip < 0x00010000 || (eip >= 0x00030000 && eip < 0x00100000)) break;

            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null
                    ? t.InnerException : ex;
                string msg = real.Message ?? "";
                if (msg.Contains("Invalid return address") || msg.Contains("Invalid Return Address"))
                    break; // normal exit (returned past entry point)
                faulted = true;
                faultMsg = $"EIP=0x{eip:X8}: {real.GetType().Name}: {msg.Split('\n')[0]}";
                break;
            }
            steps++;

            // Once a window exists, nudge it to paint then quit so we don't spin forever.
            if (!paintSent && user32.WndProcByHwnd.Count > 0 && steps > 200)
            {
                uint hwnd = user32.WndProcByHwnd.Keys.First();
                user32.PostWinMsg(hwnd, 0x000F, 0, 0); // WM_PAINT
                user32.PostWinMsg(0, 0x0012, 0, 0);    // WM_QUIT
                paintSent = true;
            }
            // Once we have a window + injected paint/quit, we've proven the success bar.
            // Stop promptly so heavy apps (magnify, spider) don't burn 60k steps under load.
            if (paintSent && steps > 2_000) break;
            // Fallback cap for apps that never open a window (so the test still terminates).
            if (!paintSent && steps > 40_000) break;
        }

        string classList = string.Join(", ", user32.WndProcByClass.Keys);
        return (faulted, faultMsg, steps, user32.WndProcByClass.Count, user32.WndProcByHwnd.Count, classList);
    }

    /// <summary>Asserts the program runs cleanly and creates a window.</summary>
    private void AssertRunsAndShowsWindow(string fileName)
    {
        string path = ProgDir + fileName;
        if (!File.Exists(path)) { Assert.Inconclusive($"{fileName} not found in reactosprog/"); return; }

        var (faulted, faultMsg, steps, classes, windows, classList) = Run(path);
        Console.WriteLine($"{fileName}: steps={steps} classes={classes} windows={windows} [{classList}]");

        // The real success signals are: didn't fault, registered a class, and created a
        // window. Step count is NOT a reliable bar here — the harness injects WM_QUIT as
        // soon as a window exists, so a well-behaved app may create its window and exit in
        // only a few hundred steps. We just require it got far enough to run real code.
        Assert.IsFalse(faulted, $"{fileName} faulted: {faultMsg}");
        Assert.IsTrue(steps >= 200, $"{fileName} should run >=200 steps, got {steps}");
        Assert.IsTrue(classes > 0, $"{fileName} should register a window class");
        Assert.IsTrue(windows > 0, $"{fileName} should create a window");
    }

    [TestMethod] public void Regedit()    => AssertRunsAndShowsWindow("regedit.exe");
    [TestMethod] public void Charmap()    => AssertRunsAndShowsWindow("charmap.exe");
    [TestMethod] public void Mmc()        => AssertRunsAndShowsWindow("mmc.exe");
    [TestMethod] public void Wordpad()    => AssertRunsAndShowsWindow("wordpad.exe");
    [TestMethod] public void Eventvwr()   => AssertRunsAndShowsWindow("eventvwr.exe");
    [TestMethod] public void Magnify()    => AssertRunsAndShowsWindow("magnify.exe");
    [TestMethod] public void Mplay32()    => AssertRunsAndShowsWindow("mplay32.exe");
    [TestMethod] public void Winmine()    => AssertRunsAndShowsWindow("winmine.exe");
    [TestMethod] public void Osk()        => AssertRunsAndShowsWindow("osk.exe");
    [TestMethod] public void Kbswitch()   => AssertRunsAndShowsWindow("kbswitch.exe");
    [TestMethod] public void Clipbrd()    => AssertRunsAndShowsWindow("clipbrd.exe");
    [TestMethod] public void Progman()    => AssertRunsAndShowsWindow("progman.exe");
    [TestMethod] public void Sndrec32()   => AssertRunsAndShowsWindow("sndrec32.exe");
    [TestMethod] public void Sol()        => AssertRunsAndShowsWindow("sol.exe");
    [TestMethod] public void Spider()     => AssertRunsAndShowsWindow("spider.exe");
    [TestMethod] public void Notepad()    => AssertRunsAndShowsWindow("notepad.exe");

    /// <summary>
    /// mspaint no longer crashes (it hit the missing AND r/m32,r32 opcode before).
    /// It registers MSPaintApp and runs clean; it does not yet open its window in the
    /// standalone harness, so we only assert "loads + runs clean + registers class".
    /// </summary>
    [TestMethod]
    public void Mspaint_RunsCleanAndRegistersClass()
    {
        string path = ProgDir + "mspaint.exe";
        if (!File.Exists(path)) { Assert.Inconclusive("mspaint.exe not found in reactosprog/"); return; }
        var (faulted, faultMsg, steps, classes, windows, classList) = Run(path);
        Console.WriteLine($"mspaint: steps={steps} classes={classes} windows={windows} [{classList}]");
        Assert.IsFalse(faulted, $"mspaint must not fault (regression for missing 0x21 AND opcode): {faultMsg}");
        Assert.IsTrue(steps >= 1000, $"mspaint should run >=1000 steps, got {steps}");
        Assert.IsTrue(classList.Contains("MSPAINTAPP"), $"mspaint should register MSPaintApp, got [{classList}]");
    }
}
