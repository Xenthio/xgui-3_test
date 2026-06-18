// Standalone MSTest suite for X86 emulator — no Sandbox dependency
// Ported from X86EmulatorTests.cs (ConCmd-based) to MSTest assertions

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;

namespace X86StandaloneTests;

[TestClass]
public class X86CoreTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    static X86Core MakeCore()
    {
        var c = new X86Core();
        c.Registers["esp"] = 0x8000;
        return c;
    }
    static X86Interpreter MakeInterpreter()
    {
        var interp = new X86Interpreter();
        // Initialize TEB/PEB so FS: accesses work
        FakeOperatingSystem.Experiments.Ambitious.X86.Handlers.SegmentPrefixHandler.InitializeTEB(interp.Core);
        interp.Core.Registers["esp"] = 0x00080000;

        return interp;
    }

    // ── RET handler ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Ret_NearReturn_RestoresEip()
    {
        var core = MakeCore();
        var handler = new RetHandler(null);

        core.Registers["esp"] = 0x1000;
        core.Push(0x12345678);
        core.Registers["eip"] = 0x2000;
        core.WriteByte(0x2000, 0xC3); // RET

        handler.Execute(core);

        Assert.AreEqual(0x12345678u, core.Registers["eip"]);
        Assert.AreEqual(0x1000u, core.Registers["esp"]);
    }

    [TestMethod]
    public void Ret_WithImm16_PopsStackBytes()
    {
        var core = MakeCore();
        var handler = new RetHandler(null);

        core.Registers["esp"] = 0x2000;
        core.Push(0xCAFEBABE);
        core.Registers["eip"] = 0x3000;
        core.WriteByte(0x3000, 0xC2); // RET imm16
        core.WriteByte(0x3001, 0x08);
        core.WriteByte(0x3002, 0x00);

        handler.Execute(core);

        Assert.AreEqual(0xCAFEBABEu, core.Registers["eip"]);
        // After RET: ESP was 0x2000 (before push), push made it 0x1FFC, pop made it 0x2000, then +8 = 0x2008
        Assert.AreEqual(0x2008u, core.Registers["esp"]);
    }

    // ── PUSH / POP ────────────────────────────────────────────────────────────

    [TestMethod]
    public void PushPop_RoundTrip_RestoresValueAndEsp()
    {
        var core = MakeCore();
        uint origEsp = core.Registers["esp"];
        core.Registers["eax"] = 0xDEADBEEF;

        core.Push(core.Registers["eax"]);
        Assert.AreEqual(origEsp - 4, core.Registers["esp"]);
        Assert.AreEqual(0xDEADBEEFu, core.ReadDword(core.Registers["esp"]));

        uint val = core.Pop();
        Assert.AreEqual(origEsp, core.Registers["esp"]);
        Assert.AreEqual(0xDEADBEEFu, val);
    }

    // ── ADD ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void AddRm32R32_RegisterToRegister_Correct()
    {
        var core = MakeCore();
        var handler = new AddRm32R32Handler();

        core.Registers["eax"] = 1;
        core.Registers["ebx"] = 2;
        core.Registers["eip"] = 0x4000;
        core.WriteByte(0x4000, 0x01); // ADD r/m32, r32
        core.WriteByte(0x4001, 0xD8); // ModRM: EAX += EBX

        handler.Execute(core);

        Assert.AreEqual(3u, core.Registers["eax"]);
    }

    // ── MOV reg, imm32 ───────────────────────────────────────────────────────

    [TestMethod]
    public void MovRegImm32_LoadsCorrectValue()
    {
        var core = MakeCore();
        var handler = new MovRegImm32Handler();

        core.Registers["eip"] = 0x5000;
        core.WriteByte(0x5000, 0xB8); // MOV EAX, imm32
        core.WriteByte(0x5001, 0x78);
        core.WriteByte(0x5002, 0x56);
        core.WriteByte(0x5003, 0x34);
        core.WriteByte(0x5004, 0x12);

        handler.Execute(core);

        Assert.AreEqual(0x12345678u, core.Registers["eax"]);
    }

    // ── JMP ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Jmp_Rel32_JumpsToCorrectAddress()
    {
        var core = MakeCore();
        var handler = new JmpHandler();

        core.Registers["eip"] = 0x6000;
        core.WriteByte(0x6000, 0xE9); // JMP rel32
        core.WriteByte(0x6001, 0x05);
        core.WriteByte(0x6002, 0x00);
        core.WriteByte(0x6003, 0x00);
        core.WriteByte(0x6004, 0x00);

        handler.Execute(core);

        // EIP = 0x6000 + 5 (opcode+rel32) + 5 (rel) = 0x600A
        Assert.AreEqual(0x600Au, core.Registers["eip"]);
    }

    // ── XOR ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void XorRm32R32_SelfXor_ZerosRegAndSetsZF()
    {
        var core = MakeCore();
        var handler = new XorRm32R32Handler();

        core.Registers["eax"] = 0xDEADBEEF;
        core.Registers["eip"] = 0x7000;
        core.WriteByte(0x7000, 0x31); // XOR r/m32, r32
        core.WriteByte(0x7001, 0xC0); // ModRM: EAX, EAX

        handler.Execute(core);

        Assert.AreEqual(0u, core.Registers["eax"]);
        Assert.IsTrue(core.ZeroFlag, "ZeroFlag should be set after XOR eax,eax");
    }

    // ── AND AL, imm8 ─────────────────────────────────────────────────────────

    [TestMethod]
    public void AndAlImm8_MasksLowByte()
    {
        var core = MakeCore();
        var handler = new AndAlImm8Handler();

        core.Registers["eax"] = 0x12345678;
        core.Registers["eip"] = 0x8000;
        core.WriteByte(0x8000, 0x24); // AND AL, imm8
        core.WriteByte(0x8001, 0xF0);

        handler.Execute(core);

        Assert.AreEqual(0x70u, core.Registers["eax"] & 0xFF);
    }

    // ── Flag instructions ─────────────────────────────────────────────────────

    [TestMethod]
    public void FlagInstructions_ClcStc_ToggleCarryFlag()
    {
        var core = MakeCore();
        var handler = new FlagInstructionHandler();

        core.CarryFlag = true;
        core.Registers["eip"] = 0x9000;
        core.WriteByte(0x9000, 0xF8); // CLC
        handler.Execute(core);
        Assert.IsFalse(core.CarryFlag, "CLC should clear carry flag");

        core.CarryFlag = false;
        core.Registers["eip"] = 0x9001;
        core.WriteByte(0x9001, 0xF9); // STC
        handler.Execute(core);
        Assert.IsTrue(core.CarryFlag, "STC should set carry flag");
    }

    // ── Stack overflow ────────────────────────────────────────────────────────

    [TestMethod]
    public void Push_NearBottom_EspWrapsOrThrows()
    {
        // The X86Core does not guard against stack underflow; ESP simply wraps.
        // This test documents that behaviour — no exception is thrown.
        var core = new X86Core();
        core.Registers["esp"] = 0x4;
        // Should not throw — just wraps or writes to low memory
        core.Push(0xDEADBEEF);
        // ESP went to 0, wrapped to 0xFFFFFFFC, or similar — either way no exception
        // Just assert the register changed (i.e. the push happened)
        Assert.AreNotEqual(0x4u, core.Registers["esp"]);
    }

    // ── Conditional jump ──────────────────────────────────────────────────────

    [TestMethod]
    public void ConditionalJump_Je_ZF1_Jumps()
    {
        var core = MakeCore();
        var handler = new ConditionalJumpHandler();

        core.ZeroFlag = true;
        core.Registers["eip"] = 0xA000;
        core.WriteByte(0xA000, 0x74); // JE rel8
        core.WriteByte(0xA001, 0x05);

        handler.Execute(core);

        Assert.AreEqual(0xA000u + 2u + 5u, core.Registers["eip"]);
    }

    [TestMethod]
    public void ConditionalJump_Jne_ZF1_DoesNotJump()
    {
        var core = MakeCore();
        var handler = new ConditionalJumpHandler();

        core.ZeroFlag = true;
        core.Registers["eip"] = 0xB000;
        core.WriteByte(0xB000, 0x75); // JNE rel8
        core.WriteByte(0xB001, 0x05);

        handler.Execute(core);

        Assert.AreEqual(0xB000u + 2u, core.Registers["eip"]);
    }

    // ── Function prologue/epilogue ────────────────────────────────────────────

    [TestMethod]
    public void FunctionPrologueEpilogue_StackBalances()
    {
        var core = MakeCore();
        uint origEsp = core.Registers["esp"];

        core.Push(0x4000); // simulate CALL push of return addr
        core.Push(core.Registers["ebp"]);
        core.Registers["ebp"] = core.Registers["esp"];
        core.Registers["esp"] -= 0x20;

        // epilogue
        core.Registers["esp"] = core.Registers["ebp"];
        core.Registers["ebp"] = core.Pop();
        uint ret = core.Pop();

        Assert.AreEqual(0x4000u, ret);
        Assert.AreEqual(origEsp, core.Registers["esp"]);
    }

    // ── Multi-instruction flow ────────────────────────────────────────────────

    [TestMethod]
    public void MultiInstructionFlow_PushAddPopSub_Correct()
    {
        var core = MakeCore();

        core.Registers["eax"] = 10;
        core.Registers["ebx"] = 20;
        core.Registers["ecx"] = 0;

        core.Push(core.Registers["eax"]);
        core.Registers["eax"] += core.Registers["ebx"];
        core.Registers["ecx"] = core.Pop();
        core.Registers["eax"] -= core.Registers["ecx"];

        Assert.AreEqual(20u, core.Registers["eax"]);
        Assert.AreEqual(10u, core.Registers["ecx"]);
    }

    // ── Stack alignment ───────────────────────────────────────────────────────

    [TestMethod]
    public void Stack_PushPopSymmetric_EspUnchanged()
    {
        var core = MakeCore();
        uint orig = core.Registers["esp"];

        for (int i = 0; i < 7; i++) core.Push((uint)i);
        for (int i = 0; i < 7; i++) core.Pop();

        Assert.AreEqual(orig, core.Registers["esp"]);
    }

    // ── Signed overflow flag ──────────────────────────────────────────────────

    [TestMethod]
    public void Add_IntMaxPlusOne_SetsOverflowFlag()
    {
        var core = MakeCore();
        var handler = new AddRm32R32Handler();

        core.Registers["eax"] = 0x7FFFFFFF;
        core.Registers["ebx"] = 1;
        core.Registers["eip"] = 0xA000;
        core.WriteByte(0xA000, 0x01);
        core.WriteByte(0xA001, 0xD8);

        handler.Execute(core);

        Assert.IsTrue(core.OverflowFlag, "OverflowFlag should be set when signed overflow occurs");
    }

    // ── Unaligned memory ─────────────────────────────────────────────────────

    [TestMethod]
    public void Memory_UnalignedDwordReadWrite_Correct()
    {
        var core = MakeCore();
        core.WriteDword(0x1003, 0xAABBCCDD);
        Assert.AreEqual(0xAABBCCDDu, core.ReadDword(0x1003));
    }

    // ── SUB flags ────────────────────────────────────────────────────────────

    [TestMethod]
    public void SubRm32R32_ZeroMinusOne_SetsSignFlagAndWraps()
    {
        var core = MakeCore();
        var handler = new SubRm32R32Handler();

        core.Registers["eax"] = 0;
        core.Registers["ebx"] = 1;
        core.Registers["eip"] = 0xC000;
        core.WriteByte(0xC000, 0x29); // SUB r/m32, r32
        core.WriteByte(0xC001, 0xD8);

        handler.Execute(core);

        Assert.AreEqual(0xFFFFFFFFu, core.Registers["eax"]);
        Assert.IsTrue(core.SignFlag, "SignFlag should be set after 0 - 1");
    }

    // ── Stack frame chain ─────────────────────────────────────────────────────

    [TestMethod]
    public void StackFrameChain_ThreeNested_UnwindsClean()
    {
        var core = MakeCore();
        uint initEsp = core.Registers["esp"];
        uint initEbp = core.Registers["ebp"];

        for (int i = 0; i < 3; i++)
        {
            core.Push(core.Registers["ebp"]);
            core.Registers["ebp"] = core.Registers["esp"];
        }

        for (int i = 0; i < 3; i++)
        {
            core.Registers["ebp"] = core.Pop();
        }

        Assert.AreEqual(initEbp, core.Registers["ebp"]);
        Assert.AreEqual(initEsp, core.Registers["esp"]);
    }

    // ── LEA ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Lea_EaxFromEbxPlusDisp8_LoadsAddress()
    {
        var core = MakeCore();
        var handler = new LeaHandler();

        core.Registers["eax"] = 0xCDCDCDCD;
        core.Registers["ebx"] = 0x1000;
        core.WriteDword(0x1050, 0xABABABAB); // value that should NOT be loaded

        core.Registers["eip"] = 0x11000;
        core.WriteByte(0x11000, 0x8D); // LEA
        core.WriteByte(0x11001, 0x43); // ModRM: EAX, [EBX+disp8]
        core.WriteByte(0x11002, 0x50); // disp8 = 0x50

        handler.Execute(core);

        Assert.AreEqual(0x1050u, core.Registers["eax"], "LEA should load effective address, not memory value");
    }

    // ── REP MOVSB ────────────────────────────────────────────────────────────

    [TestMethod]
    public void RepMovsb_CopiesFourBytes()
    {
        var core = MakeCore();
        var handler = new StringOperationsHandler();

        core.Registers["esi"] = 0x1000;
        core.Registers["edi"] = 0x2000;
        core.Registers["ecx"] = 4;
        core.DirectionFlag = false;
        core.Registers["eip"] = 0xD000;
        core.WriteByte(0xD000, 0xF3); // REP
        core.WriteByte(0xD001, 0xA4); // MOVSB

        core.WriteByte(0x1000, 0x11);
        core.WriteByte(0x1001, 0x22);
        core.WriteByte(0x1002, 0x33);
        core.WriteByte(0x1003, 0x44);

        handler.Execute(core); // consume REP prefix
        handler.Execute(core); // execute MOVSB * ECX

        Assert.AreEqual(0x11, (int)core.ReadByte(0x2000));
        Assert.AreEqual(0x22, (int)core.ReadByte(0x2001));
        Assert.AreEqual(0x33, (int)core.ReadByte(0x2002));
        Assert.AreEqual(0x44, (int)core.ReadByte(0x2003));
        Assert.AreEqual(0u, core.Registers["ecx"]);
        Assert.AreEqual(0x1004u, core.Registers["esi"]);
        Assert.AreEqual(0x2004u, core.Registers["edi"]);
    }

    // ── CallX86Function + WndProc dispatch ────────────────────────────────────

    [TestMethod]
    public void CallX86Function_SimpleReturnValue()
    {
        // A tiny function: MOV EAX, 0xDEADBEEF ; RET 4  (1 stdcall arg)
        var interp = MakeInterpreter();
        var core   = interp.Core;

        uint funcAddr = 0x5000;
        core.WriteByte(funcAddr+0, 0xB8);          // MOV EAX, imm32
        core.WriteDword(funcAddr+1, 0xDEADBEEF);
        core.WriteByte(funcAddr+5, 0xC2);           // RET imm16
        core.WriteWord(funcAddr+6, 4);              // clean 1 arg (4 bytes)

        uint result = interp.CallX86Function(funcAddr, 0u);
        Assert.AreEqual(0xDEADBEEFu, result, "CallX86Function should return EAX");
    }

    [TestMethod]
    public void CallX86Function_WndProcDispatch()
    {
        // WndProc: reads msg from [ESP+8] into EAX, then RET 16 (4 stdcall args)
        // Frame on entry: [esp+0]=retAddr, [esp+4]=hwnd, [esp+8]=msg, [esp+12]=wParam, [esp+16]=lParam
        var interp  = MakeInterpreter();
        var core    = interp.Core;

        uint wndProc = 0x6000;
        core.WriteByte(wndProc+0, 0x8B); // MOV EAX, [ESP+8]
        core.WriteByte(wndProc+1, 0x44);
        core.WriteByte(wndProc+2, 0x24);
        core.WriteByte(wndProc+3, 0x08);
        core.WriteByte(wndProc+4, 0xC2); // RET 16
        core.WriteWord(wndProc+5, 16);

        uint hwnd = 0x7F000005;
        var user32 = interp.APIEmulators.OfType<User32Emulator>().First();
        user32.WndProcByHwnd[hwnd] = wndProc;

        // Write MSG struct at 0x3000
        uint msgPtr = 0x3000;
        core.WriteDword(msgPtr+0,  hwnd);
        core.WriteDword(msgPtr+4,  0x000F); // WM_PAINT
        core.WriteDword(msgPtr+8,  0);
        core.WriteDword(msgPtr+12, 0);
        core.WriteDword(msgPtr+16, 0);
        core.WriteDword(msgPtr+20, 0);
        core.WriteDword(msgPtr+24, 0);

        // Push MSG ptr + fake ret addr for stdcall convention
        core.Registers["esp"] = 0x00080000;
        core.Push(msgPtr);
        core.Push(0xFFFF1234);

        bool called = user32.TryCall("DispatchMessageA", core, interp, out uint result);
        Assert.IsTrue(called, "DispatchMessageA should be registered");
        Assert.AreEqual(0x000Fu, result, "WndProc should echo msg (WM_PAINT=0x0F) into EAX");
    }

    [TestMethod]
    public void MessageQueue_PostAndGetMessage()
    {
        var interp = MakeInterpreter();
        var core   = interp.Core;
        var user32 = interp.APIEmulators.OfType<User32Emulator>().First();

        uint hwnd   = 0x7F000010;
        uint msgPtr = 0x4000;

        user32.PostWinMsg(hwnd, 0x0111, 1, 0); // WM_COMMAND
        Assert.AreEqual(1, user32.MessageQueue.Count, "Queue should have 1 message");

        // Set up stack for GetMessageA(lpMsg, hWnd, min, max)
        core.Registers["esp"] = 0x00080000;
        core.Push(0);      // max
        core.Push(0);      // min
        core.Push(0);      // hWndFilter
        core.Push(msgPtr); // lpMsg
        core.Push(0xFFFF1234); // ret addr

        bool ok = user32.TryCall("GetMessageA", core, interp, out uint ret);
        Assert.IsTrue(ok, "GetMessageA should be registered");
        Assert.AreEqual(1u, ret, "Non-WM_QUIT returns 1");
        Assert.AreEqual(0, user32.MessageQueue.Count, "Queue should be empty after dequeue");

        Assert.AreEqual(hwnd,   core.ReadDword(msgPtr+0), "MSG.hwnd mismatch");
        Assert.AreEqual(0x0111u, core.ReadDword(msgPtr+4), "MSG.message should be WM_COMMAND");
    }

    [TestMethod]
    public void MessageQueue_WmQuitReturnsZero()
    {
        var interp = MakeInterpreter();
        var core   = interp.Core;
        var user32 = interp.APIEmulators.OfType<User32Emulator>().First();

        uint msgPtr = 0x4100;
        user32.PostWinMsg(0, 0x0012, 0, 0); // WM_QUIT

        core.Registers["esp"] = 0x00080000;
        core.Push(0); core.Push(0); core.Push(0); core.Push(msgPtr);
        core.Push(0xFFFF1234);

        user32.TryCall("GetMessageA", core, interp, out uint ret);
        Assert.AreEqual(0u, ret, "WM_QUIT should return 0 from GetMessage");
    }

}