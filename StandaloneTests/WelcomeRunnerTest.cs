// Standalone test: attempt to run WELCOME.EXE (Win95) in the x86 emulator

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
public class WelcomeRunnerTests
{
    private const string WelcomePath =
        @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\95prog\WELCOME.EXE";

    private static bool WelcomeExists => File.Exists(WelcomePath);

    [TestMethod]
    public void Welcome_LoadsPeWithoutException()
    {
        if (!WelcomeExists) Assert.Inconclusive("WELCOME.EXE not found at expected path");
        var interp = new X86Interpreter();
        byte[] bytes = File.ReadAllBytes(WelcomePath);
        bool loaded = interp.LoadExecutable(bytes, WelcomePath);
        Assert.IsTrue(loaded, "LoadExecutable should return true for a valid PE");
        Console.WriteLine($"Entry point: 0x{interp.Core.Registers["eip"]:X8}");
    }

    [TestMethod]
    public void Welcome_RunsForSomeStepsWithoutFault()
    {
        if (!WelcomeExists) Assert.Inconclusive("WELCOME.EXE not found at expected path");

        var interp = new X86Interpreter();
        byte[] bytes = File.ReadAllBytes(WelcomePath);
        bool loaded = interp.LoadExecutable(bytes, WelcomePath);
        Assert.IsTrue(loaded, "PE must load");

        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;
        var user32 = interp.APIEmulators.OfType<User32Emulator>().First();

        const int MaxSteps = 1_000_000;
        int steps = 0;
        bool faulted = false;
        string faultMsg = null;
        bool quitSent = false;

        var lastEips = new Queue<uint>();

        while (steps < MaxSteps)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE)
                break;

            lastEips.Enqueue(eip);
            if (lastEips.Count > 20) lastEips.Dequeue();

            try
            {
                iset.ExecuteNext(core, interp);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (msg.Contains("Invalid return address") || msg.Contains("Invalid Return Address"))
                {
                    Console.WriteLine($"[Normal exit at EIP=0x{eip:X8}] {msg}");
                    break;
                }
                faulted = true;
                faultMsg = $"EIP=0x{eip:X8}: {ex.GetType().Name}: {msg}";
                break;
            }
            steps++;

            if (!quitSent && user32.WndProcByHwnd.Count > 0 && steps > 10_000)
            {
                user32.PostWinMsg(0, 0x0012, 0, 0); // WM_QUIT
                quitSent = true;
            }

            if (quitSent && steps > 50_000)
                break;
        }

        Console.WriteLine($"\n=== Run summary ===");
        Console.WriteLine($"Steps: {steps}");
        Console.WriteLine($"WndProcByClass: {string.Join(", ", user32.WndProcByClass.Select(kv => $"'{kv.Key}'=0x{kv.Value:X8}"))}");
        Console.WriteLine($"WndProcByHwnd: {user32.WndProcByHwnd.Count} windows");
        Console.WriteLine($"MessageQueue remaining: {user32.MessageQueue.Count}");
        Console.WriteLine($"Faulted: {faulted} — {faultMsg}");
        Console.WriteLine($"Last 20 EIPs: {string.Join(" ", lastEips.Select(e => $"0x{e:X8}"))}");

        Assert.IsFalse(faulted, $"Emulator should not fault: {faultMsg}");
        // Note: welcome.exe may exit quickly depending on stub behavior; just verify no fault + some execution
        Assert.IsTrue(steps >= 50, $"Should run at least 50 steps, got {steps}");
    }
}
