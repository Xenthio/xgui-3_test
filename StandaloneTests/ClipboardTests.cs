using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;

namespace StandaloneTests;

[TestClass]
public class ClipboardTests
{
	[TestMethod]
	public void Clipboard_TextRoundtrip_ASCII()
	{
		User32Emulator.SetClipboardTextForTest("Hello, clipboard!");
		var read = User32Emulator.GetClipboardTextForTest();
		Assert.AreEqual("Hello, clipboard!", read);
	}

	[TestMethod]
	public void Clipboard_TextRoundtrip_Unicode()
	{
		User32Emulator.SetClipboardTextForTest("Test 日本語 中文");
		var read = User32Emulator.GetClipboardTextForTest();
		Assert.AreEqual("Test 日本語 中文", read);
	}

	[TestMethod]
	public void Clipboard_ConstantsMatchWin32()
	{
		Assert.AreEqual(1u, User32Emulator.CF_TEXT);
		Assert.AreEqual(2u, User32Emulator.CF_BITMAP);
		Assert.AreEqual(13u, User32Emulator.CF_UNICODETEXT);
		Assert.AreEqual(15u, User32Emulator.CF_HDROP);
	}
}
