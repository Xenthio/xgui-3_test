// Standalone test: attempt to run calc.exe (NT4 Windows Calculator) in the x86 emulator

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
public class CalcRunnerTests
{
    private const string CalcPath =
        @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt4prog\calc.exe";

    private static bool CalcExists => File.Exists(CalcPath);

    [TestMethod]
    public void Calc_LoadsPeWithoutException()
    {
        if (!CalcExists) Assert.Inconclusive("calc.exe not found at expected path");

        var interp = new X86Interpreter();
        byte[] bytes = File.ReadAllBytes(CalcPath);
        bool loaded = interp.LoadExecutable(bytes, CalcPath);

        Assert.IsTrue(loaded, "LoadExecutable should return true for a valid PE");
        uint eip = interp.Core.Registers["eip"];
        Assert.AreNotEqual(0u, eip, "PE should set EIP to the entry point");
        Console.WriteLine($"Entry point: 0x{eip:X8}   ImageBase: 0x{interp.Core.Registers["eip"] - 0x5C10:X8}");
    }

    [TestMethod]
    public void Calc_RunsForSomeStepsWithoutFault()
    {
        if (!CalcExists) Assert.Inconclusive("calc.exe not found at expected path");

        var interp = new X86Interpreter();
        byte[] bytes = File.ReadAllBytes(CalcPath);
        bool loaded = interp.LoadExecutable(bytes, CalcPath);
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

            if (!paintSent && steps > 50000)
            {
                // Inject WM_QUIT after enough init steps even if no window was created
                // (calc may be in message loop waiting for input)
                if (user32.WndProcByHwnd.Count > 0)
                {
                    uint hwnd = user32.WndProcByHwnd.Keys.First();
                    user32.PostWinMsg(hwnd, 0x000F, 0, 0); // WM_PAINT
                }
                user32.PostWinMsg(0, 0x0012, 0, 0); // WM_QUIT
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
        Assert.IsTrue(user32.WndProcByClass.Count > 0,
            "RegisterClassExW should have been called — WndProcByClass should be populated");
        // WndProcByHwnd may be 0 in standalone tests (CreateWindowExW needs full XGUI)
        Console.WriteLine($"Windows created: {user32.WndProcByHwnd.Count}");
    }
}
