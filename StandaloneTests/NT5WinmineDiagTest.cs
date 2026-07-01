using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

namespace X86StandaloneTests;

[TestClass]
public class NT5WinmineDiagTests
{
    private const string NT5Path = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\2000prog\winmine.exe";
    private static readonly string OutPath = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt5_winmine_render.png";
    private static readonly string LogFile = @"E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\nt5_winmine_diag.log";

    [TestMethod]
    public void NT5_Winmine_RenderToPNG()
    {
        if (!File.Exists(NT5Path)) Assert.Inconclusive("not found");

        // Suppress excessive logging
        Log.Silent = true;

        var interp = new X86Interpreter();
        interp.LoadExecutable(File.ReadAllBytes(NT5Path), NT5Path);
        SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Push(0xFFFFFFFF);

        var core = interp.Core;
        var iset = interp.InstructionSet;
        var u32  = interp.APIEmulators.OfType<User32Emulator>().First();

        GDICanvas fakeCanvas = null;
        uint mainHwnd = 0;

        bool faulted = false; string faultMsg = null;
        for (int steps = 0; steps < 500_000; steps++)
        {
            uint eip = core.Registers["eip"];
            if (eip == 0 || eip == 0xFFFFFFFF || eip == 0xFFFFFFFE) break;
            if (eip < 0x00010000 || (eip >= 0x00030000 && eip < 0x00100000)) break;

            // Inject canvas once window is registered
            if (fakeCanvas == null && u32.WndProcByHwnd.Count > 0)
            {
                mainHwnd = u32.WndProcByHwnd.Keys.First();
                fakeCanvas = GDI32Emulator.CreateWindowCanvas(mainHwnd, 162, 244, null);
            }

            try { iset.ExecuteNext(core, interp); }
            catch (Exception ex)
            {
                var real = ex is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                if (real.Message.Contains("Invalid return address") || real.Message.Contains("Invalid Return Address")) break;
                faulted = true; faultMsg = $"EIP=0x{eip:X8}: {real.GetType().Name}: {real.Message}";
                break;
            }
        }

        Log.Silent = false;

        Assert.IsFalse(faulted, faultMsg);
        Assert.IsNotNull(fakeCanvas, "Canvas not injected");

        int w = fakeCanvas.Width, h = fakeCanvas.Height;
        int greyPx = 0, nonGreyPx = 0;
        for (int i = 0; i < fakeCanvas.Pixels.Length; i += 4)
        {
            byte r = fakeCanvas.Pixels[i], g = fakeCanvas.Pixels[i+1], b = fakeCanvas.Pixels[i+2];
            if (r == 0xC0 && g == 0xC0 && b == 0xC0) greyPx++;
            else nonGreyPx++;
        }

        // Save as raw PPM for easy viewing
        using var f = File.Create(OutPath.Replace(".png", ".ppm"));
        var header = System.Text.Encoding.ASCII.GetBytes($"P6\n{w} {h}\n255\n");
        f.Write(header);
        for (int i = 0; i < fakeCanvas.Pixels.Length; i += 4)
            f.Write(new[] { fakeCanvas.Pixels[i], fakeCanvas.Pixels[i+1], fakeCanvas.Pixels[i+2] });

        File.WriteAllText(LogFile, $"Canvas: {w}x{h}, grey={greyPx}, non-grey={nonGreyPx}\nSaved to {OutPath.Replace(".png",".ppm")}");
        Console.WriteLine($"Canvas: {w}x{h}, grey={greyPx}, non-grey={nonGreyPx}");

        Assert.IsTrue(nonGreyPx > 500, $"Expected painted pixels. grey={greyPx} non-grey={nonGreyPx}");
    }
}
