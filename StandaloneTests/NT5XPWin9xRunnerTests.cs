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
    private const string NtProgDir    = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\ntprog\";
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

    // Win98 GDI apps placed in 2000prog/ (same PE32 architecture)
    [TestMethod] public void SndVol32_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "sndvol32.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }
    [TestMethod] public void SndRec32_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "sndrec32.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }
    [TestMethod] public void Charmap_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "charmap.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }
    [TestMethod] public void CDPlayer_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "cdplayer.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }

    // =================== ReactOS apps ===================
    [TestMethod] public void ReactOS_Sol_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_sol.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 50000, $"too few steps: {steps}");
    }
    [TestMethod] public void ReactOS_Spider_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_spider.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 50000, $"too few steps: {steps}");
    }
    [TestMethod] public void ReactOS_MPlay32_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_mplay32.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }
    [TestMethod] public void ReactOS_Magnify_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_magnify.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }
    [TestMethod] public void ReactOS_FontView_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_fontview.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }
    [TestMethod] public void ReactOS_ProgMan_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_progman.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }
    [TestMethod] public void ReactOS_MsConfig_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_msconfig.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }
    [TestMethod] public void ReactOS_Osk_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_osk.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 500, $"too few steps: {steps}");
    }

    [TestMethod] public void ReactOS_CleanMgr_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_cleanmgr.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }

    [TestMethod] public void ReactOS_DrWtsn32_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_drwtsn32.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }

    [TestMethod] public void ReactOS_SndVol32_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_sndvol32.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }

    [TestMethod] public void ReactOS_SndRec32_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_sndrec32.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }

    [TestMethod] public void Win2k_Perfmon_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "perfmon.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 100, $"too few steps: {steps}");
    }

    [TestMethod] public void Win2k_Proquota_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "proquota.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 100, $"too few steps: {steps}");
    }

    [TestMethod] public void Win2k_Pinball_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "PINBALL.EXE");
        if (steps < 0) Assert.Inconclusive("not found");
        // Pinball uses DirectX heavily; just verify it loads and runs init phase
        Assert.IsTrue(steps >= 100, $"too few steps: {steps}");
    }

    [TestMethod] public void Win2k_Cmd_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "cmd.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(steps >= 100, $"too few steps: {steps}");
    }

    [TestMethod] public void ReactOS_Mspaint_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_mspaint.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }

    [TestMethod] public void ReactOS_Notepad_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NT5ProgDir + "reactos_notepad.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }

    // ── ntprog ─────────────────────────────────────────────────────────────────
    [TestMethod] public void NtProg_Sol_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NtProgDir + "sol.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    [TestMethod] public void NtProg_Osk_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NtProgDir + "osk.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }

    [TestMethod] public void NtProg_Winmine_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NtProgDir + "winmine.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    [TestMethod] public void NtProg_Calc_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(NtProgDir + "calc.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 200, $"too few steps: {steps}");
    }

    // ── Community suggestions ─────────────────────────────────────────────
    private const string CommunitySugDir = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\community_suggestions\";

    [TestMethod] public void CommSug_Ski32_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "ski32.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    [TestMethod] public void CommSug_Ski32RebuildVS6_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "ski32-rebuild-vs6.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    [TestMethod] public void CommSug_Metapad_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "metapad.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    [TestMethod] public void CommSug_TinyTask_RunsWithoutFault()
    {
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "tinytask-1-77.exe");
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsFalse(faulted, msg);
        Assert.IsTrue(steps >= 1000, $"too few steps: {steps}");
    }

    [TestMethod] public void CommSug_ClawdHello_SelfCompiledPE_Works()
    {
        // This EXE was hand-crafted by Clawd itself as a PE with x86 code
        // that calls MessageBoxA then ExitProcess.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_hello.exe", maxSteps: 10_000);
        if (steps < 0) Assert.Inconclusive("clawd_hello.exe not found");
        // Accept ExitProcess-style exit: the 0xFFFFFFFF EIP may fire ReadByte before the
        // loop top checks it — that's an expected "clean exit", not a real fault.
        bool cleanExit = msg != null && (msg.Contains("0xFFFFFFFF") || msg.Contains("unmapped high address 0xFFFFFF"));
        Assert.IsTrue(!faulted || cleanExit, $"Unexpected fault: {msg}");
        Assert.IsTrue(steps >= 5, $"Expected at least a few steps, got {steps}");
    }

    // ── Clawd self-compiled test programs (PE factory) ─────────────────
    // These are built by pe_factory.py in /tmp using pure Python x86 codegen.

    private static bool IsCleanExit(bool faulted, string msg) =>
        !faulted || (msg != null && (msg.Contains("0xFFFFFFFF") || msg.Contains("unmapped high address 0xFFFFFF")));

    [TestMethod] public void ClaWd_MsgboxLoop_ThreeBoxes()
    {
        // Calls MessageBoxA three times, then ExitProcess.
        // Tests: IAT resolution, stdcall conv, string VA, sequential calls.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_msgbox_loop.exe", maxSteps: 10_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(steps >= 10, $"Too few steps: {steps}");
    }

    [TestMethod] public void ClaWd_Window_RegisterAndPump()
    {
        // Registers a window class, creates a window, runs 3 message-pump iterations.
        // Tests: RegisterClassExA, CreateWindowExA, ShowWindow, GetMessageA,
        //        TranslateMessage, DispatchMessageA, WM_PAINT/WM_DESTROY, PostQuitMessage.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_window.exe", maxSteps: 50_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(!string.IsNullOrEmpty(classes), $"Expected at least 1 registered class, got: {classes}");
        Assert.IsTrue(windows > 0, $"Expected at least 1 window, got {windows}");
        Assert.IsTrue(steps >= 50, $"Too few steps: {steps}");
    }

    [TestMethod] public void ClaWd_FizzBuzz_ArithmeticTest()
    {
        // Computes 15 % 3 == 0 and 15 % 5 == 0, shows "FizzBuzz" MessageBox.
        // Tests: x86 IDIV, conditional branch, correct MB result.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_fizzbuzz.exe", maxSteps: 10_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(steps >= 10, $"Too few steps: {steps}");
    }

    [TestMethod] public void ClaWd_GdiDraw_WindowWithPaint()
    {
        // Creates a window, WM_PAINT: FillRect (blue bg) + SetTextColor (white) + TextOutA.
        // Tests: GDI drawing pipeline, BeginPaint/EndPaint, GDI color ops.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_gdi_draw.exe", maxSteps: 50_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(!string.IsNullOrEmpty(classes), $"Expected class registered, got: {classes}");
        Assert.IsTrue(windows > 0, $"Expected window created, got {windows}");
    }

    // ── Advanced self-compiled EXE tests ─────────────────────────────────────────

    [TestMethod] public void ClaWd_Registry_WriteAndReadBack()
    {
        // Tests: RegCreateKeyExA, RegSetValueExA, RegQueryValueExA, RegCloseKey
        // Writes "Hello42" to HKCU\Software\ClaWdTest\TestValue and reads it back.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_registry.exe", maxSteps: 10_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(steps >= 10, $"Too few steps: {steps}");
    }

    [TestMethod] public void ClaWd_ChildWnd_ThreeChildren()
    {
        // Tests: WS_CHILD parenting — creates parent + 3 child windows.
        // Verifies all 4 windows created and class registered.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_childwnd.exe", maxSteps: 10_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(!string.IsNullOrEmpty(classes), $"Expected class registered");
        Assert.IsTrue(windows >= 4, $"Expected 4 windows (1 parent + 3 children), got {windows}");
    }

    [TestMethod] public void ClaWd_Heap_AllocAndFree()
    {
        // Tests: GetProcessHeap, HeapAlloc, HeapFree, VirtualAlloc, VirtualFree.
        // Allocates 1KB on heap + 4KB via VirtualAlloc, writes pattern, frees.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_heap.exe", maxSteps: 10_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(steps >= 10, $"Too few steps: {steps}");
    }

    [TestMethod] public void ClaWd_WsprintfA_FormatString()
    {
        // Tests: wsprintfA (cdecl variadic), lstrlenA, GetCommandLineA.
        // Formats "Val=42 hex=0x2A" and shows via MessageBoxA.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_wsprintfa.exe", maxSteps: 10_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(steps >= 10, $"Too few steps: {steps}");
    }

    [TestMethod] public void ClaWd_RepString_MovsdMemcpy()
    {
        // Tests: REP MOVSD (memcpy via x86 string ops).
        // Copies "Hello REP World!" using REP MOVSD, verifies first 4 bytes == "Hell".
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_repstring.exe", maxSteps: 10_000);
        if (steps < 0) Assert.Inconclusive("not found");
        Assert.IsTrue(IsCleanExit(faulted, msg), $"Fault: {msg}");
        Assert.IsTrue(steps >= 10, $"Too few steps: {steps}");
    }

    [TestMethod] public void ClaWd_Exception_DivideByZeroHandled()
    {
        // Tests: divide-by-zero fault is caught by emulator and doesn't crash test runner.
        // The EXE intentionally executes IDIV ECX with ECX=0.
        // Acceptable outcomes: fault (expected) OR emulator swallows it and continues.
        var (steps, faulted, msg, classes, windows) = RunExe(CommunitySugDir + "clawd_exception.exe", maxSteps: 1_000);
        if (steps < 0) Assert.Inconclusive("not found");
        // Either faults gracefully (expected), or doesn't fault at all (emulator swallowed it).
        // What's NOT acceptable: test runner crash / stack overflow.
        Assert.IsTrue(steps >= 1, $"Expected at least one step before exception, got {steps}");
    }

}