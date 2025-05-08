using FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;
using Sandbox;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Tests;
public class X86EmulatorTests
{
	[ConCmd( "xguitest_x86_run_tests" )]
	public static void RunTests()
	{
		TestRetHandler();
		TestPushPop();
		TestAddRm32R32();
		TestMovRegImm32();
		TestJmpHandler();
		TestXorRm32R32();
		TestAndAlImm8();
		TestFlagInstructionHandler();
		TestStackOverflow();
		TestConditionalJump();
		TestFunctionPrologueEpilogue();
		TestMultiInstructionFlow();
		TestStackAlignment();
		TestSignedOverflowFlag();
		TestUnalignedMemoryAccess();
		TestSibAddressing();
		TestStackFrameChain();
		TestPopUnderflow();
		TestSelfModifyingCode();
		TestFlagsAfterSub();
		TestRepMovsb();
	}

	public static void TestRetHandler()
	{
		var core = new X86Core();
		var handler = new RetHandler();

		core.Registers["esp"] = 0x1000;
		core.Push( 0x12345678 );
		core.Registers["eip"] = 0x2000;
		core.WriteByte( 0x2000, 0xC3 ); // RET

		handler.Execute( core );

		Log.Info( "RET Test:" );
		Log.Info( core.Registers["eip"] == 0x12345678 ? "PASS" : "FAIL" );
		Log.Info( core.Registers["esp"] == 0x1000 ? "PASS" : "FAIL" );

		core.Registers["esp"] = 0x2000;
		core.Push( 0xCAFEBABE );
		core.Registers["eip"] = 0x3000;
		core.WriteByte( 0x3000, 0xC2 ); // RET imm16
		core.WriteByte( 0x3001, 0x08 ); // imm16 = 0x0008
		core.WriteByte( 0x3002, 0x00 );

		handler.Execute( core );

		Log.Info( "RET imm16 Test:" );
		Log.Info( core.Registers["eip"] == 0xCAFEBABE ? "PASS" : "FAIL" );
		Log.Info( core.Registers["esp"] == 0x2008 ? "PASS" : "FAIL" );
	}

	public static void TestPushPop()
	{
		var core = new X86Core();
		uint origEsp = 0x8000;
		core.Registers["esp"] = origEsp;
		core.Registers["eax"] = 0xDEADBEEF;

		// PUSH EAX
		core.Push( core.Registers["eax"] );
		Log.Info( "PUSH Test:" );
		Log.Info( core.Registers["esp"] == origEsp - 4 ? "PASS" : "FAIL" );
		Log.Info( core.ReadDword( core.Registers["esp"] ) == 0xDEADBEEF ? "PASS" : "FAIL" );

		// POP into EBX
		core.Registers["ebx"] = 0;
		core.Registers["ebx"] = core.Pop();
		Log.Info( "POP Test:" );
		Log.Info( core.Registers["esp"] == origEsp ? "PASS" : "FAIL" );
		Log.Info( core.Registers["ebx"] == 0xDEADBEEF ? "PASS" : "FAIL" );
	}

	public static void TestAddRm32R32()
	{
		var core = new X86Core();
		var handler = new AddRm32R32Handler();

		// ADD EAX, EBX (EAX = 1, EBX = 2)
		core.Registers["eax"] = 1;
		core.Registers["ebx"] = 2;
		core.Registers["eip"] = 0x4000;
		core.WriteByte( 0x4000, 0x01 ); // ADD r/m32, r32
		core.WriteByte( 0x4001, 0xD8 ); // ModRM: 11 011 000 (EAX, EBX)

		handler.Execute( core );

		Log.Info( "ADD EAX, EBX Test:" );
		Log.Info( core.Registers["eax"] == 3 ? "PASS" : "FAIL" );
	}

	public static void TestMovRegImm32()
	{
		var core = new X86Core();
		var handler = new MovRegImm32Handler();

		// MOV EAX, 0x12345678
		core.Registers["eip"] = 0x5000;
		core.WriteByte( 0x5000, 0xB8 ); // MOV EAX, imm32
		core.WriteByte( 0x5001, 0x78 );
		core.WriteByte( 0x5002, 0x56 );
		core.WriteByte( 0x5003, 0x34 );
		core.WriteByte( 0x5004, 0x12 );

		handler.Execute( core );

		Log.Info( "MOV EAX, imm32 Test:" );
		Log.Info( core.Registers["eax"] == 0x12345678 ? "PASS" : "FAIL" );
	}

	public static void TestJmpHandler()
	{
		var core = new X86Core();
		var handler = new JmpHandler();

		// JMP rel32: EIP = 0x6000, jump +5
		core.Registers["eip"] = 0x6000;
		core.WriteByte( 0x6000, 0xE9 ); // JMP rel32
		core.WriteByte( 0x6001, 0x05 );
		core.WriteByte( 0x6002, 0x00 );
		core.WriteByte( 0x6003, 0x00 );
		core.WriteByte( 0x6004, 0x00 );

		handler.Execute( core );

		Log.Info( "JMP rel32 Test:" );
		Log.Info( core.Registers["eip"] == 0x6000 + 5 + 5 ? "PASS" : "FAIL" ); // opcode + rel32 + offset
	}

	public static void TestXorRm32R32()
	{
		var core = new X86Core();
		var handler = new XorRm32R32Handler();

		// XOR EAX, EAX (should zero EAX and set ZF)
		core.Registers["eax"] = 0xDEADBEEF;
		core.Registers["eip"] = 0x7000;
		core.WriteByte( 0x7000, 0x31 ); // XOR r/m32, r32
		core.WriteByte( 0x7001, 0xC0 ); // ModRM: 11 000 000 (EAX, EAX)

		handler.Execute( core );

		Log.Info( "XOR EAX, EAX Test:" );
		Log.Info( core.Registers["eax"] == 0 ? "PASS" : "FAIL" );
		Log.Info( core.ZeroFlag ? "PASS" : "FAIL" );
	}

	public static void TestAndAlImm8()
	{
		var core = new X86Core();
		var handler = new AndAlImm8Handler();

		// AND AL, 0xF0 (EAX = 0x12345678, AL = 0x78)
		core.Registers["eax"] = 0x12345678;
		core.Registers["eip"] = 0x8000;
		core.WriteByte( 0x8000, 0x24 ); // AND AL, imm8
		core.WriteByte( 0x8001, 0xF0 );

		handler.Execute( core );

		Log.Info( "AND AL, imm8 Test:" );
		Log.Info( (core.Registers["eax"] & 0xFF) == 0x70 ? "PASS" : "FAIL" );
	}

	public static void TestFlagInstructionHandler()
	{
		var core = new X86Core();
		var handler = new FlagInstructionHandler();

		// CLC (Clear Carry Flag)
		core.CarryFlag = true;
		core.Registers["eip"] = 0x9000;
		core.WriteByte( 0x9000, 0xF8 ); // CLC

		handler.Execute( core );

		Log.Info( "CLC Test:" );
		Log.Info( !core.CarryFlag ? "PASS" : "FAIL" );

		// STC (Set Carry Flag)
		core.CarryFlag = false;
		core.Registers["eip"] = 0x9001;
		core.WriteByte( 0x9001, 0xF9 ); // STC

		handler.Execute( core );

		Log.Info( "STC Test:" );
		Log.Info( core.CarryFlag ? "PASS" : "FAIL" );
	}

	public static void TestStackOverflow()
	{
		var core = new X86Core();
		core.Registers["esp"] = 0x4; // Near bottom of memory

		try
		{
			core.Push( 0xDEADBEEF );
			Log.Info( "Stack Overflow Test: FAIL (no exception)" );
		}
		catch
		{
			Log.Info( "Stack Overflow Test: PASS (exception thrown)" );
		}
	}

	public static void TestConditionalJump()
	{
		var core = new X86Core();
		var handler = new ConditionalJumpHandler();

		// JE rel8 (ZF = 1, should jump)
		core.ZeroFlag = true;
		core.Registers["eip"] = 0xA000;
		core.WriteByte( 0xA000, 0x74 ); // JE rel8
		core.WriteByte( 0xA001, 0x05 ); // +5

		handler.Execute( core );

		Log.Info( "JE (ZF=1) Test:" );
		Log.Info( core.Registers["eip"] == 0xA000 + 2 + 5 ? "PASS" : "FAIL" );

		// JNE rel8 (ZF = 1, should not jump)
		core.ZeroFlag = true;
		core.Registers["eip"] = 0xB000;
		core.WriteByte( 0xB000, 0x75 ); // JNE rel8
		core.WriteByte( 0xB001, 0x05 ); // +5

		handler.Execute( core );

		Log.Info( "JNE (ZF=1) Test:" );
		Log.Info( core.Registers["eip"] == 0xB000 + 2 ? "PASS" : "FAIL" );
	}
	public static void TestFunctionPrologueEpilogue()
	{
		var core = new X86Core();
		// Simulate CALL: push return address
		core.Registers["esp"] = 0x8000;
		core.Push( 0x4000 ); // Return address

		// Prologue: PUSH EBP; MOV EBP, ESP
		core.Push( core.Registers["ebp"] );
		core.Registers["ebp"] = core.Registers["esp"];

		// Allocate local space
		core.Registers["esp"] -= 0x20;

		// Epilogue: MOV ESP, EBP; POP EBP; RET
		core.Registers["esp"] = core.Registers["ebp"];
		core.Registers["ebp"] = core.Pop();
		uint ret = core.Pop();

		Log.Info( "Function Prologue/Epilogue Test:" );
		Log.Info( ret == 0x4000 ? "PASS" : "FAIL" );
		Log.Info( core.Registers["esp"] == 0x8000 ? "PASS" : "FAIL" );
	}
	public static void TestMultiInstructionFlow()
	{
		var core = new X86Core();
		// Setup registers and memory
		core.Registers["eax"] = 10;
		core.Registers["ebx"] = 20;
		core.Registers["ecx"] = 0;
		core.Registers["esp"] = 0x9000;

		// Simulate: PUSH EAX; ADD EAX, EBX; POP ECX; SUB EAX, ECX
		core.Push( core.Registers["eax"] );
		core.Registers["eax"] += core.Registers["ebx"];
		core.Registers["ecx"] = core.Pop();
		core.Registers["eax"] -= core.Registers["ecx"];

		Log.Info( "Multi-Instruction Flow Test:" );
		Log.Info( core.Registers["eax"] == 20 ? "PASS" : "FAIL" );
		Log.Info( core.Registers["ecx"] == 10 ? "PASS" : "FAIL" );
		Log.Info( core.Registers["esp"] == 0x9000 ? "PASS" : "FAIL" );
	}

	public static void TestStackAlignment()
	{
		var core = new X86Core();
		core.Registers["esp"] = 0x8000;
		for ( int i = 0; i < 7; i++ )
			core.Push( (uint)i );
		for ( int i = 0; i < 7; i++ )
			core.Pop();
		Log.Info( "Stack Alignment Test:" );
		Log.Info( core.Registers["esp"] == 0x8000 ? "PASS" : "FAIL" );
	}

	public static void TestSignedOverflowFlag()
	{
		var core = new X86Core();
		var handler = new AddRm32R32Handler();

		// ADD EAX, EBX (EAX = int.MaxValue, EBX = 1)
		core.Registers["eax"] = 0x7FFFFFFF;
		core.Registers["ebx"] = 1;
		core.Registers["eip"] = 0xA000;
		core.WriteByte( 0xA000, 0x01 ); // ADD r/m32, r32
		core.WriteByte( 0xA001, 0xD8 ); // ModRM: 11 011 000 (EAX, EBX)
		handler.Execute( core );

		Log.Info( "Signed Overflow Flag Test (ADD):" );
		Log.Info( core.OverflowFlag ? "PASS" : "FAIL" );
	}

	public static void TestUnalignedMemoryAccess()
	{
		var core = new X86Core();
		core.WriteDword( 0x1003, 0xAABBCCDD );
		uint val = core.ReadDword( 0x1003 );
		Log.Info( "Unaligned Memory Access Test:" );
		Log.Info( val == 0xAABBCCDD ? "PASS" : "FAIL" );
	}

	public static void TestSibAddressing()
	{
		var core = new X86Core();
		var handler = new MovR32RmHandler();

		core.Registers["eax"] = 0;
		core.Registers["ebx"] = 0x2000;
		core.Registers["ecx"] = 3;
		core.WriteDword( 0x2000 + 3 * 4 + 8, 0xDEADBEEF );

		// MOV EAX, [EBX+ECX*4+8]
		core.Registers["eip"] = 0xB000;
		core.WriteByte( 0xB000, 0x8B ); // MOV r32, r/m32
		core.WriteByte( 0xB001, 0x84 ); // ModRM: mod=00, reg=000 (EAX), rm=100 (SIB)
		core.WriteByte( 0xB002, 0x8B ); // SIB: scale=2 (4), index=1 (ECX), base=3 (EBX)
		core.WriteByte( 0xB003, 0x08 ); // disp32 = 8
		core.WriteByte( 0xB004, 0x00 );
		core.WriteByte( 0xB005, 0x00 );
		core.WriteByte( 0xB006, 0x00 );

		handler.Execute( core );

		Log.Info( "SIB Addressing Test:" );
		Log.Info( core.Registers["eax"] == 0xDEADBEEF ? "PASS" : "FAIL" );
	}

	public static void TestStackFrameChain()
	{
		var core = new X86Core();
		core.Registers["esp"] = 0x9000;
		core.Registers["ebp"] = 0x0;
		// Simulate 3 nested calls with prologues
		for ( int i = 0; i < 3; i++ )
		{
			core.Push( core.Registers["ebp"] );
			core.Registers["ebp"] = core.Registers["esp"];
		}
		// Unwind
		bool pass = true;
		for ( int i = 0; i < 3; i++ )
		{
			uint prevEbp = core.Pop();
			pass &= (core.Registers["ebp"] == core.Registers["esp"]);
			core.Registers["ebp"] = prevEbp;
		}
		Log.Info( "Stack Frame Chain Test:" );
		Log.Info( pass ? "PASS" : "FAIL" );
	}

	public static void TestPopUnderflow()
	{
		var core = new X86Core();
		core.Registers["esp"] = 0x00090000; // Above stack
		try
		{
			core.Pop();
			Log.Info( "POP Underflow Test: FAIL (no exception)" );
		}
		catch
		{
			Log.Info( "POP Underflow Test: PASS (exception thrown)" );
		}
	}

	public static void TestSelfModifyingCode()
	{
		var core = new X86Core();
		uint codeAddr = 0x40000;
		core.MarkMemoryAsCode( codeAddr, 0x100 );
		try
		{
			core.WriteByte( codeAddr, 0x90 ); // NOP
			Log.Info( "Self-Modifying Code Test: FAIL (no exception)" );
		}
		catch
		{
			Log.Info( "Self-Modifying Code Test: PASS (exception thrown)" );
		}
	}

	public static void TestFlagsAfterSub()
	{
		var core = new X86Core();
		var handler = new SubRm32R32Handler();

		// SUB EAX, EBX (EAX = 0, EBX = 1)
		core.Registers["eax"] = 0;
		core.Registers["ebx"] = 1;
		core.Registers["eip"] = 0xC000;
		core.WriteByte( 0xC000, 0x29 ); // SUB r/m32, r32
		core.WriteByte( 0xC001, 0xD8 ); // ModRM: 11 011 000 (EAX, EBX)
		handler.Execute( core );

		Log.Info( "Flags After SUB Test:" );
		Log.Info( core.Registers["eax"] == 0xFFFFFFFF ? "PASS" : "FAIL" );
		Log.Info( core.SignFlag ? "PASS" : "FAIL" );
	}

	public static void TestRepMovsb()
	{
		var core = new X86Core();
		var handler = new StringOperationsHandler();

		// Setup: Copy 4 bytes from 0x1000 to 0x2000
		core.Registers["esi"] = 0x1000;
		core.Registers["edi"] = 0x2000;
		core.Registers["ecx"] = 4;
		core.Registers["eip"] = 0xD000;
		core.WriteByte( 0xD000, 0xF3 ); // REP prefix
		core.WriteByte( 0xD001, 0xA4 ); // MOVSB

		// Fill source
		core.WriteByte( 0x1000, 0x11 );
		core.WriteByte( 0x1001, 0x22 );
		core.WriteByte( 0x1002, 0x33 );
		core.WriteByte( 0x1003, 0x44 );

		handler.Execute( core );

		bool pass = core.ReadByte( 0x2000 ) == 0x11 &&
					core.ReadByte( 0x2001 ) == 0x22 &&
					core.ReadByte( 0x2002 ) == 0x33 &&
					core.ReadByte( 0x2003 ) == 0x44;

		Log.Info( "REP MOVSB Test:" );
		Log.Info( pass ? "PASS" : "FAIL" );
	}
}

