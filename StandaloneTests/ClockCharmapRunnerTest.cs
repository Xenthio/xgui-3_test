// Standalone tests for clock.exe and charmap.exe (NT4) in the x86 emulator
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

[TestClass]
public class ClockRunnerTests
{
    private const string ExePath =
        @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt4prog\clock.exe";
    private static bool ExeExists => File.Exists(ExePath);

    private static (int steps, bool faulted, string faultMsg, User32Emulator user32) RunExe(int maxSteps = 200_000)
    {
        var interp = new X86Interpreter();
        byte[] bytes = File.ReadAllBytes(ExePath);
        interp.LoadExecutable(bytes, ExePath);
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
                if (ex.Message.Contains("Invalid return address") || ex.Message.Contains("Invalid Return Address"))
                { Console.WriteLine($"[Normal exit at EIP=0x{eip:X8}] {ex.Message}"); break; }
                faulted = true;
                faultMsg = $"EIP=0x{eip:X8}: {ex.GetType().Name}: {ex.Message}";
                break;
            }
            steps++;

            if (!quitSent && steps > 50_000)
            {
                if (user32.WndProcByHwnd.Count > 0)
                    user32.PostWinMsg(user32.WndProcByHwnd.Keys.First(), 0x000F, 0, 0); // WM_PAINT
                user32.PostWinMsg(0, 0x0012, 0, 0); // WM_QUIT
                quitSent = true;
            }
        }

        Console.WriteLine($"Steps: {steps}, WndProcByClass: [{string.Join(", ", user32.WndProcByClass.Select(kv => $"'{kv.Key}'=0x{kv.Value:X8}"))}], Windows: {user32.WndProcByHwnd.Count}, Faulted: {faulted} {faultMsg}");
        Console.WriteLine($"Last EIP: 0x{core.Registers["eip"]:X8}");
        return (steps, faulted, faultMsg, user32);
    }

    [TestMethod]
    public void Clock_LoadsPe()
    {
        if (!ExeExists) Assert.Inconclusive("clock.exe not found");
        var interp = new X86Interpreter();
        bool loaded = interp.LoadExecutable(File.ReadAllBytes(ExePath), ExePath);
        Assert.IsTrue(loaded);
        Console.WriteLine($"Entry: 0x{interp.Core.Registers["eip"]:X8}");
    }

    [TestMethod]
    public void Clock_RunsForSomeStepsWithoutFault()
    {
        if (!ExeExists) Assert.Inconclusive("clock.exe not found");
        var (steps, faulted, faultMsg, user32) = RunExe();
        Assert.IsFalse(faulted, $"Should not fault: {faultMsg}");
        Assert.IsTrue(steps >= 1000, $"Should run at least 1000 steps, got {steps}");
    }
}

[TestClass]
public class CharmapRunnerTests
{
    private const string ExePath =
        @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt4prog\charmap.exe";
    private static bool ExeExists => File.Exists(ExePath);

    private static (int steps, bool faulted, string faultMsg, User32Emulator user32) RunExe(int maxSteps = 200_000)
    {
        var interp = new X86Interpreter();
        byte[] bytes = File.ReadAllBytes(ExePath);
        interp.LoadExecutable(bytes, ExePath);
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
                if (ex.Message.Contains("Invalid return address") || ex.Message.Contains("Invalid Return Address"))
                { Console.WriteLine($"[Normal exit at EIP=0x{eip:X8}] {ex.Message}"); break; }
                faulted = true;
                faultMsg = $"EIP=0x{eip:X8}: {ex.GetType().Name}: {ex.Message}";
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

        Console.WriteLine($"Steps: {steps}, WndProcByClass: [{string.Join(", ", user32.WndProcByClass.Select(kv => $"'{kv.Key}'=0x{kv.Value:X8}"))}], Windows: {user32.WndProcByHwnd.Count}, Faulted: {faulted} {faultMsg}");
        Console.WriteLine($"Last EIP: 0x{core.Registers["eip"]:X8}");
        return (steps, faulted, faultMsg, user32);
    }

    [TestMethod]
    public void Charmap_LoadsPe()
    {
        if (!ExeExists) Assert.Inconclusive("charmap.exe not found");
        var interp = new X86Interpreter();
        bool loaded = interp.LoadExecutable(File.ReadAllBytes(ExePath), ExePath);
        Assert.IsTrue(loaded);
        Console.WriteLine($"Entry: 0x{interp.Core.Registers["eip"]:X8}");
    }

    [TestMethod]
    public void Charmap_RunsForSomeStepsWithoutFault()
    {
        if (!ExeExists) Assert.Inconclusive("charmap.exe not found");
        var (steps, faulted, faultMsg, user32) = RunExe();
        Assert.IsFalse(faulted, $"Should not fault: {faultMsg}");
        Assert.IsTrue(steps >= 1000, $"Should run at least 1000 steps, got {steps}");
    }
}
