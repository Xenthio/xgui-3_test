using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

// TEMPORARY scanner — runs every PE in the organized prog folders and reports status.
[TestClass]
public class ScanCandidatesTest
{
    private const string DataDir = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\";
    private static System.Text.StringBuilder _report = new();
    private static void R(string s) { _report.AppendLine(s); }

    private static (int steps, bool faulted, string faultMsg, string classes, int windows) RunExe(
        string exePath, int maxSteps = 60_000)
    {
        if (!File.Exists(exePath)) return (-1, false, "not found", "", 0);

        X86Interpreter interp;
        try
        {
            interp = new X86Interpreter();
            interp.LoadExecutable(File.ReadAllBytes(exePath), exePath);
            SegmentPrefixHandler.InitializeTEB(interp.Core);
            interp.Core.Push(0xFFFFFFFF);
        }
        catch (Exception ex)
        {
            return (0, true, $"LOAD: {ex.GetType().Name}: {ex.Message?.Split('\n')[0]}", "", 0);
        }

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
            if (eip < 0x00010000 || (eip >= 0x00030000 && eip < 0x00100000)) break;
            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                if (real.Message.Contains("Invalid return address") || real.Message.Contains("Invalid Return Address"))
                { break; }
                faulted = true;
                faultMsg = $"EIP=0x{eip:X8}: {real.GetType().Name}: {real.Message?.Split('\n')[0]}";
                break;
            }
            steps++;
            if (!quitSent && steps > 30_000)
            {
                if (user32.WndProcByHwnd.Count > 0)
                    user32.PostWinMsg(user32.WndProcByHwnd.Keys.First(), 0x000F, 0, 0);
                user32.PostWinMsg(0, 0x0012, 0, 0);
                quitSent = true;
            }
        }

        string classes = string.Join(", ", user32.WndProcByClass.Select(kv => $"'{kv.Key}'"));
        return (steps, faulted, faultMsg, classes, user32.WndProcByHwnd.Count);
    }

    private void ScanFolder(string folder)
    {
        string dir = DataDir + folder;
        if (!Directory.Exists(dir)) { R($"[{folder}] not found"); return; }
        var results = new List<(string name, int steps, bool faulted, string msg, string classes, int windows)>();
        foreach (var path in Directory.GetFiles(dir, "*.exe").OrderBy(p => p))
        {
            var (steps, faulted, msg, classes, windows) = RunExe(path);
            results.Add((Path.GetFileName(path), steps, faulted, msg, classes, windows));
        }
        R($"========== {folder} ({results.Count} exes) ==========");
        R("CLEAN (window created, no fault):");
        foreach (var r in results.Where(r => !r.faulted && r.windows > 0).OrderByDescending(r => r.steps))
            R($"  OK  {r.name,-20} steps={r.steps,-7} win={r.windows} [{r.classes}]");
        R("RAN, NO WINDOW:");
        foreach (var r in results.Where(r => !r.faulted && r.windows == 0).OrderByDescending(r => r.steps))
            R($"  --  {r.name,-20} steps={r.steps,-7} [{r.classes}]");
        R("FAULTED:");
        foreach (var r in results.Where(r => r.faulted).OrderByDescending(r => r.steps))
            R($"  XX  {r.name,-20} steps={r.steps,-7} win={r.windows} {r.msg}");
    }

    [TestMethod]
    public void Scan()
    {
        _report.Clear();
        ScanFolder("reactosprog");
        ScanFolder("w2kprog");
        string outPath = DataDir + "scan_results.txt";
        File.WriteAllText(outPath, _report.ToString());
        Console.WriteLine("SCAN_RESULTS_WRITTEN: " + outPath);
    }
}
