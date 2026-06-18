// Standalone test: attempt to run winmine.exe (NT4) in the x86 emulator

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
public class WinmineRunnerTests
{
    private const string WinminePath =
        @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt4prog\winmine.exe";

    private static bool WinmineExists => File.Exists(WinminePath);

    [TestMethod]
    public void Winmine_LoadsPeWithoutException()
    {
        if (!WinmineExists) Assert.Inconclusive("winmine.exe not found at expected path");

        var interp = new X86Interpreter();
        byte[] bytes = File.ReadAllBytes(WinminePath);
        bool loaded = interp.LoadExecutable(bytes, WinminePath);

        Assert.IsTrue(loaded, "LoadExecutable should return true for a valid PE");
        uint eip = interp.Core.Registers["eip"];
        Assert.AreNotEqual(0u, eip, "PE should set EIP to the entry point");
        Console.WriteLine($"Entry point: 0x{eip:X8}   ImageBase/HeapStart: 0x{interp.HeapStart:X8}");
    }

    [TestMethod]
    public void Winmine_RunsForSomeStepsWithoutFault()
    {
        if (!WinmineExists) Assert.Inconclusive("winmine.exe not found at expected path");

        var interp = new X86Interpreter();
        byte[] bytes = File.ReadAllBytes(WinminePath);
        bool loaded = interp.LoadExecutable(bytes, WinminePath);
        Assert.IsTrue(loaded, "PE must load");

        // Prime stack as ExecuteAsync does
        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;
        var user32 = interp.APIEmulators.OfType<User32Emulator>().First();

        const int MaxSteps = 1_000_000;
        int steps = 0;
        bool faulted = false;
        string faultMsg = null;
        bool paintSent = false;

        // Ring buffer of last 20 EIP values for debugging
        var lastEips = new Queue<uint>();

        while (steps < MaxSteps)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE)
                break;

            // Track last 20 EIPs
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

            if (!paintSent && user32.WndProcByHwnd.Count > 0 && steps > 100)
            {
                uint hwnd = user32.WndProcByHwnd.Keys.First();
                user32.PostWinMsg(hwnd, 0x000F, 0, 0); // WM_PAINT
                user32.PostWinMsg(0, 0x0012, 0, 0);    // WM_QUIT
                paintSent = true;
            }

            if (paintSent && steps > 500_000)
                break;
        }

        Console.WriteLine($"\n=== Run summary ===");
        Console.WriteLine($"Steps: {steps}");
        Console.WriteLine($"WndProcByClass: {string.Join(", ", user32.WndProcByClass.Select(kv => $"'{kv.Key}'=0x{kv.Value:X8}"))}");
        Console.WriteLine($"WndProcByHwnd: {user32.WndProcByHwnd.Count} windows");
        Console.WriteLine($"MessageQueue remaining: {user32.MessageQueue.Count}");
        Console.WriteLine($"Faulted: {faulted} — {faultMsg}");
        Console.WriteLine($"Paint/quit injected: {paintSent}");
        Console.WriteLine($"Last 20 EIPs: {string.Join(" ", lastEips.Select(e => $"0x{e:X8}"))}");

        Assert.IsFalse(faulted, $"Emulator should not fault: {faultMsg}");
        Assert.IsTrue(steps >= 1000, $"Should run at least 1000 steps, got {steps}");
        // RegisterClassA must be called
        Assert.IsTrue(user32.WndProcByClass.Count > 0,
            "RegisterClassA should have been called — WndProcByClass should be populated");
    }
}
