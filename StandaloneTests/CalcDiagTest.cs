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
public class CalcDiagTests
{
    private const string CalcPath = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\2000prog\calc.exe";
    private static readonly string LogFile = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\calc_diag.log";

    [TestMethod]
    public void Calc_Smoke()
    {
        if (!File.Exists(CalcPath)) Assert.Inconclusive("not found");
        // NOTE: CreateDialogParamW / TryBuildDialog is GUI-only (User32Emulator.GUI.cs excluded from standalone).
        // This test just verifies calc.exe doesn't fault before reaching CreateDialogParamW, and
        // registers its window class (SciCalc) successfully.
        var logs = new List<string>();
        Log.Silent = false;
        // Redirect console to capture log output
        var sw = new System.IO.StringWriter();
        Console.SetOut(sw);

        var interp = new X86Interpreter();
        interp.LoadExecutable(File.ReadAllBytes(CalcPath), CalcPath);
        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;
        var u32  = interp.APIEmulators.OfType<User32Emulator>().First();

        bool faulted = false; string faultMsg = null;
        bool gotWindow = false;
        int steps = 0;
        const int maxSteps = 1_000_000;

        for (; steps < maxSteps; steps++)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE) break;
            if (eip < 0x00010000 || (eip >= 0x00030000 && eip < 0x00100000)) break;

            if (!gotWindow && u32.WndProcByHwnd.Count > 0) gotWindow = true;
            if (gotWindow && steps > 100_000) break;

            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                if (real.Message.Contains("Invalid return address") || real.Message.Contains("Invalid Return Address")) break;
                faulted = true;
                faultMsg = $"EIP=0x{eip:X8}: {real.GetType().Name}: {real.Message}";
                break;
            }
        }

        File.WriteAllLines(LogFile, logs);

        // Restore console and parse logs
        Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        string captured = sw.ToString();
        logs.AddRange(captured.Split('\n'));
        File.WriteAllText(LogFile, captured);

        // Report summary
        var warnings = logs.Count(l => l.Contains("[WARN]"));
        var errors   = logs.Count(l => l.Contains("[ERROR]"));
        var infos    = logs.Count(l => l.Contains("[INFO]"));
        var createDialogLine = logs.FirstOrDefault(l => l.Contains("TryBuildDialog") || l.Contains("CreateDialogParam"));
        var initDialogLine   = logs.FirstOrDefault(l => l.Contains("WM_INITDIALOG") || l.Contains("INITDIALOG"));

        Console.WriteLine($"steps={steps} window={gotWindow} faulted={faulted}");
        Console.WriteLine($"infos={infos} warnings={warnings} errors={errors}");
        Console.WriteLine($"CreateDialog: {createDialogLine}");
        Console.WriteLine($"WM_INITDIALOG: {initDialogLine}");
        if (faultMsg != null) Console.WriteLine($"Fault: {faultMsg}");
        Console.WriteLine($"Log: {LogFile}");

        Assert.IsTrue(!faulted || faultMsg == null || faultMsg.Contains("Invalid return"),
            $"Calc faulted unexpectedly: {faultMsg}\nFirst 20 logs:\n{string.Join("\n", logs.Take(20))}");

        // In standalone, RegisterClassExW('SciCalc') is the best we can confirm
        Assert.IsTrue(logs.Any(l => l.Contains("SciCalc")),
            $"Calc never registered SciCalc class\nFirst 20 logs:\n{string.Join("\n", logs.Take(20))}");
    }
}
