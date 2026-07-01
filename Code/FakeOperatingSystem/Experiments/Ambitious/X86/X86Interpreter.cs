using FakeDesktop;
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using Sandbox;
using Sandbox.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

// Might rename to PEInterpreter or something like that
public partial class X86Interpreter
{
	public readonly X86Core Core = new();
	public readonly X86InstructionSet InstructionSet = new();
	public readonly List<APIEmulator> APIEmulators = new();
	public T GetEmulator<T>() where T : APIEmulator
	{
		foreach ( var e in APIEmulators ) if ( e is T t ) return t;
		return null;
	}
	public Dictionary<string, uint> Imports = new();
	private uint _entryPoint;
	public string ExecutableName { get; private set; } = "VIRTUAL.EXE";
	public Dictionary<string, string> ImportSourceDlls = new();

	public uint HeapStart = 0x00400000; // Default heap start address
	public uint ModuleBase = 0x00400000; // Actual PE image base

	/// <summary>Standard output stream — wired from X86PEProcess.LaunchOptions when console app.</summary>
	public System.IO.TextWriter StandardOutput { get; set; }
	/// <summary>Standard input stream — wired from X86PEProcess.LaunchOptions when console app.</summary>
	public System.IO.TextReader StandardInput { get; set; }
	// APIs in this set use thiscall (callee-cleanup); JMP handler auto-detects arg count
	public readonly System.Collections.Generic.HashSet<string> ThiscallExports = new();

	public Dictionary<(uint hInstance, uint uID), string> StringResources = new();
	/// <summary>Tracks the language ID that was used to populate each StringResources entry, for multilingual precedence.</summary>
	public Dictionary<(uint hInstance, uint uID), uint> StringResourceLangs = new();
	public Dictionary<(uint hInstance, uint uID), byte[]> DialogResources { get; } = new();
	public Dictionary<(uint hInstance, uint uID), byte[]> BitmapResources { get; } = new();
	public Dictionary<(uint hInstance, uint uID), byte[]> IconResources { get; } = new(); // For RT_ICON
	public Dictionary<(uint hInstance, uint uID), byte[]> GroupIconResources { get; } = new(); // For RT_GROUP_ICON
	public Dictionary<(uint hInstance, string name), byte[]> GroupIconResourcesByName { get; } = new(); // Named icon resources
	public Dictionary<(uint hInstance, uint uID), byte[]> MenuResources { get; } = new(); // For RT_MENU


	public delegate void MessageBoxHandler( string title, string message, MessageBoxIcon icon = MessageBoxIcon.Error, MessageBoxButtons buttons = MessageBoxButtons.AbortRetryIgnore );
	public event MessageBoxHandler OnHaltWithMessageBox;

	public X86Interpreter()
	{
		APIEmulators.Add( new User32Emulator() );
		APIEmulators.Add( new Kernel32Emulator() );
		APIEmulators.Add( new MsvcrtEmulator() );
		APIEmulators.Add( new Shell32Emulator() );
		APIEmulators.Add( new Advapi32Emulator() );
		APIEmulators.Add( new GDI32Emulator() );
		APIEmulators.Add( new WinMMEmulator() );
		APIEmulators.Add( new CardsEmulator() );
		APIEmulators.Add( new MFC42uEmulator() );
		APIEmulators.Add( new Comctl32Emulator() );
		APIEmulators.Add( new Ole32Emulator() );
		APIEmulators.Add( new GetUNameEmulator() );
		APIEmulators.Add( new NtdllEmulator() );
		APIEmulators.Add( new CrtdllEmulator() );

		// === Miscellaneous ===
		InstructionSet.RegisterHandler( new Handlers.NopHandler() );
		InstructionSet.RegisterHandler( new Handlers.PushaPopsHandler() );
		InstructionSet.RegisterHandler( new Handlers.HltHandler() );
		InstructionSet.RegisterHandler( new Handlers.SignExtendHandler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeFEHandler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode00Handler() );

		// === Arithmetic ===
		InstructionSet.RegisterHandler( new Handlers.AddRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AddR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.SubRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.SubR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AdcRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.AluRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.AdcR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AdcRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AdcEaxImmHandler() );
		InstructionSet.RegisterHandler( new Handlers.AddEaxImmHandler() );
		InstructionSet.RegisterHandler( new Handlers.XorAlImmHandler() );
		InstructionSet.RegisterHandler( new Handlers.AluEaxImmHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovSregHandler() );
		InstructionSet.RegisterHandler( new Handlers.SegRegPushPopHandler() );
		InstructionSet.RegisterHandler( new Handlers.AluR8Rm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.SbbR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.SbbRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.FpuStubHandler() );
		InstructionSet.RegisterHandler( new Handlers.XorRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.XorRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.XorHandler() );
		InstructionSet.RegisterHandler( new Handlers.OrR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.OrEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.OrRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AndR32Rm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AndRm32R32Handler() ); // 0x21 AND r/m32, r32 (was missing)
		InstructionSet.RegisterHandler( new Handlers.AndAlImm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.AndEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.AndRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.BCDArithmeticHandler() );
		InstructionSet.RegisterHandler( new Handlers.CmpHandler() );
		InstructionSet.RegisterHandler( new Handlers.CmpAlImm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.CmpR8Rm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.CmpEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode80Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode81Handler() );
		InstructionSet.RegisterHandler( new Handlers.Opcode83Handler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeF6Handler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeF7Handler() );
		InstructionSet.RegisterHandler( new Handlers.ImulImmHandler() ); // 0x69 / 0x6B IMUL r32, r/m32, imm

		// === Data Movement ===
		InstructionSet.RegisterHandler( new Handlers.MovRegImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovRmR32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovR32RmHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovRm32Imm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovRm8Imm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovEaxMemHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovAlMoffs32Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovReg8Imm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovR8Rm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.MovMoffs32EaxHandler() );
		InstructionSet.RegisterHandler( new Handlers.LesHandler() );
		InstructionSet.RegisterHandler( new Handlers.PopRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.PushRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.PushImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.PushImm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.SegmentPrefixHandler() );
		InstructionSet.RegisterHandler( new Handlers.PopEsHandler() );
		InstructionSet.RegisterHandler( new Handlers.MovReg8SSRm8Handler() );
		InstructionSet.RegisterHandler( new Handlers.LeaHandler() );
		InstructionSet.RegisterHandler( new Handlers.XchgHandler() );

		// === Control Flow ===
		InstructionSet.RegisterHandler( new Handlers.CallRel32Handler() );
		InstructionSet.RegisterHandler( new Handlers.JmpHandler() );
		InstructionSet.RegisterHandler( new Handlers.ConditionalJumpHandler() );
		InstructionSet.RegisterHandler( new Handlers.RetHandler( this ) );
		InstructionSet.RegisterHandler( new Handlers.LoopHandler() );
		InstructionSet.RegisterHandler( new Handlers.ExtendedOpcodeHandler() );
		InstructionSet.RegisterHandler( new Handlers.OpcodeFFHandler( this ) );

		// === Logic/Bitwise ===
		InstructionSet.RegisterHandler( new Handlers.TestRm32R32Handler() );
		InstructionSet.RegisterHandler( new Handlers.TestRm8R8Handler() );
		InstructionSet.RegisterHandler( new Handlers.TestEaxImm32Handler() );
		InstructionSet.RegisterHandler( new Handlers.FlagInstructionHandler() );
		InstructionSet.RegisterHandler( new Handlers.ShiftRotateHandler() );
		InstructionSet.RegisterHandler( new Handlers.OperandSizePrefixHandler() );

		// === Stack/Frame ===
		InstructionSet.RegisterHandler( new Handlers.LeaveHandler() );
		InstructionSet.RegisterHandler( new Handlers.IncDecRegHandler() );
		InstructionSet.RegisterHandler( new Handlers.CdqHandler() );

		// === String/Memory ===
		InstructionSet.RegisterHandler( new Handlers.StringOperationsHandler() );

		// === Port/IO ===
		InstructionSet.RegisterHandler( new Handlers.PortIOHandler() );

		// === Testing ===

		//InstructionSet.RegisterHandler( new Handlers.TestingHandlerNotReal() );
	}

	public bool LoadExecutable( byte[] fileBytes, string path = null )
	{
		// Extract exe name from path if available
		if ( !string.IsNullOrEmpty( path ) )
		{
			ExecutableName = System.IO.Path.GetFileName( path ).ToUpper();
		}

		var loader = new PELoader();
		bool loaded = loader.Load( fileBytes, Core, out _entryPoint, out Imports, out ImportSourceDlls, out HeapStart, out uint moduleBase );
		// Set EIP to the entry point immediately so callers can start executing
		if ( loaded ) { Core.Registers["eip"] = _entryPoint; ModuleBase = moduleBase; }

		if ( loader.ParseAllResources( fileBytes, out var resources ) )
		{
			// Assuming hInstance for the main executable is its base address (HeapStart is used as a proxy here, typically 0x00400000)
			// In a multi-module scenario, hInstance would vary.
			uint hInstance = moduleBase; // Use actual PE image base for resource keys

			foreach ( var res in resources )
			{
				if ( res.Type == 2 ) // RT_BITMAP
				{
					BitmapResources[(hInstance, res.Name)] = res.Data;
					Core.LogVerbose( $"Loaded bitmap resource: ID=0x{res.Name:X8}, Size={res.Data.Length} bytes, hInstance=0x{hInstance:X8}" );
				}
				else if ( res.Type == 3 ) // RT_ICON
				{
					IconResources[(hInstance, res.Name)] = res.Data;
					Core.LogVerbose( $"Loaded icon resource (RT_ICON): ID=0x{res.Name:X8}, Size={res.Data.Length} bytes, hInstance=0x{hInstance:X8}" );
				}
				else if ( res.Type == 5 ) // RT_DIALOG
				{
					DialogResources[(hInstance, res.Name)] = res.Data;
					Core.LogVerbose( $"Loaded dialog resource: ID=0x{res.Name:X8}, Size={res.Data.Length} bytes, hInstance=0x{hInstance:X8}" );
				}
				else if ( res.Type == 6 ) // RT_STRING
				{
					// Only load this language if it's preferred over what we already have.
					// Priority: en-US (0x0409) > language-neutral (0x0000) > any.
					// This prevents multilingual ReactOS builds from loading Chinese strings last.
					bool isEnUS = res.Language == 0x0409;
					bool isNeutral = res.Language == 0x0000;
					using var ms = new System.IO.MemoryStream( res.Data );
					using var br = new System.IO.BinaryReader( ms );
					for ( uint i = 0; i < 16; i++ ) // String tables are bundled in blocks of 16
					{
						if ( ms.Position + 2 > ms.Length )
							break;

						ushort strlen = br.ReadUInt16();
						string value = "";
						if ( strlen > 0 )
						{
							if ( ms.Position + strlen * 2 > ms.Length )
								break;

							byte[] strBytes = br.ReadBytes( strlen * 2 );
							value = Encoding.Unicode.GetString( strBytes );
							uint strKey = (res.Name - 1) * 16 + i;
							// Only write if en-US, or if neutral and not already have en-US, or if no entry yet
							bool alreadyHave = StringResources.ContainsKey( (hInstance, strKey) );
							// Track whether we already loaded en-US for this key
							bool alreadyEnUS = StringResourceLangs.TryGetValue( (hInstance, strKey), out uint existingLang ) && existingLang == 0x0409;
							if ( isEnUS || !alreadyHave || (isNeutral && !alreadyEnUS) )
							{
								StringResources[(hInstance, strKey)] = value;
								StringResourceLangs[(hInstance, strKey)] = res.Language;
								Core.LogVerbose( $"Loaded string resource: ID=0x{strKey:X8}, Lang=0x{res.Language:X4}, Value=\"{value}\", hInstance=0x{hInstance:X8}" );
							}
						}
					}
				}
				else if ( res.Type == 14 ) // RT_GROUP_ICON
				{
					GroupIconResources[(hInstance, res.Name)] = res.Data;
					if ( res.StringName != null )
						GroupIconResourcesByName[(hInstance, res.StringName)] = res.Data;
					Core.LogVerbose( $"Loaded group icon resource (RT_GROUP_ICON): ID=0x{res.Name:X8}, Name='{res.StringName}', Size={res.Data.Length} bytes, hInstance=0x{hInstance:X8}" );
				}
				else if ( res.Type == 4 ) // RT_MENU
				{
					MenuResources[(hInstance, res.Name)] = res.Data;
					Core.LogVerbose( $"Loaded menu resource (RT_MENU): ID=0x{res.Name:X8}, Size={res.Data.Length} bytes, hInstance=0x{hInstance:X8}" );
				}
			}
		}

		return loaded;
	}

	[ConVar( "xguitest_x86_log_eip" )]
	public static bool EIPLogging { get; set; } = false;
	private bool _haltASAP = false;
	uint maxInstructions = 0xFFFFFFFF;
	int yieldEvery = 200;
	public SyncTask ThisSyncTask { get; private set; }

	// Suspend/resume: set _suspendUntil to a TCS task before stepping; the loop awaits it.
	private Task _suspendUntil = null;
	public void SuspendForTask( Task t ) => _suspendUntil = t;
	public void Resume() => _suspendUntil = null;

	public async void ExecuteAsync()
	{
		// Initialize TEB/PEB in memory before first instruction
		Handlers.SegmentPrefixHandler.InitializeTEB( Core );

		Core.Push( 0xFFFFFFFF ); // Address of our final return, this will be used if we hit a RET without anything else in the stack, which we can assume is our final RET
		Core.Registers["eip"] = _entryPoint;
		int i = 0;

		for ( i = 0; i < maxInstructions; i++ )
		{
			// Check for program exit
			if ( Core.Registers["eip"] == 0xFFFFFFFF )
			{
				Log.Info( "Program execution completed via final RET" );
				break;
			}

			if ( _haltASAP )
			{
				Log.Info( "Execution halted by user request." );
				break;
			}

			// Suspended (e.g. waiting for EndDialog) — keep yielding until resumed
			if ( _suspendUntil != null && !_suspendUntil.IsCompleted )
			{
				await _suspendUntil;
				_suspendUntil = null;
			}

			if ( EIPLogging )
			{
				Log.Info( $"EIP: 0x{Core.Registers["eip"]:X8}" );
			}

			try
			{
				InstructionSet.ExecuteNext( Core, this );
			}
			catch ( System.Exception ex )
			{
				Log.Error( $"Execution error at EIP 0x{Core.Registers["eip"]:X8}: {ex.Message}" );
				if ( ex.InnerException != null ) Log.Error( ex.InnerException );
				Log.Error( ex.StackTrace );
				if ( !ex.Message.StartsWith( "!" ) )
				{
					MessageBoxUtility.ShowCustom(
						$"Execution error at 0x{Core.Registers["eip"]:X8}: {ex.Message}",
						"Execution Error",
						MessageBoxIcon.Error,
						MessageBoxButtons.OK
					);
				}
				break;
			}

			// Yield to UI every N instructions
			if ( (i % yieldEvery) == 0 )
				await Task.Yield();

			// Every 100000, log EIP and executable name
			if ( (i % 100000) == 0 )
			{
				Log.Info( $"EIP: 0x{Core.Registers["eip"]:X8} - Still executing {ExecutableName}!" );
			}
		}

		if ( i >= maxInstructions )
		{
			Log.Warning( "Execution reached maximum instruction limit." );
		}
		else
		{
			Log.Info( $"Executed {i} instructions." );
		}
		OnFinish?.Invoke();
	}

	public void Halt()
	{
		_haltASAP = true;
	}

	/// <summary>
	/// Call an emulated x86 function at <paramref name="addr"/> with stdcall args.
	/// Saves and restores EIP/ESP/EBP so it is safe to call from inside an API stub.
	/// Returns EAX (the function's return value).
	/// </summary>
	public uint CallX86Function( uint addr, params uint[] args )
	{
		if ( addr == 0 ) { Log.Warning( "CallX86Function: addr is 0, ignoring" ); return 0; }

		// Save caller context
		uint savedEip = Core.Registers["eip"];
		uint savedEsp = Core.Registers["esp"];
		uint savedEbp = Core.Registers["ebp"];

		// Push args right-to-left (stdcall)
		for ( int k = args.Length - 1; k >= 0; k-- )
			Core.Push( args[k] );

		// Push a sentinel return address so we know when to stop
		const uint sentinel = 0xFFFFFFFE;
		Core.Push( sentinel );

		Core.Registers["eip"] = addr;

		// Run until we hit the sentinel or an error
		for ( int i = 0; i < 10_000_000; i++ )
		{
			if ( Core.Registers["eip"] == sentinel )
				break;
			if ( Core.Registers["eip"] == 0xFFFFFFFF )
				break;
			try
			{
				InstructionSet.ExecuteNext( Core, this );
			}
			catch ( System.Exception ex )
			{
				Log.Error( $"CallX86Function: exception at EIP 0x{Core.Registers["eip"]:X8}: {ex.Message}" );
				break;
			}
		}

		uint result = Core.Registers["eax"];

		// Restore caller context
		Core.Registers["eip"] = savedEip;
		Core.Registers["esp"] = savedEsp;
		Core.Registers["ebp"] = savedEbp;

		return result;
	}

	public Action OnFinish;

	public void DumpMemory( uint start, uint length )
	{
		var memdump = "";
		for ( uint i = 0; i < length; i++ )
		{
			byte b = Core.ReadByte( start + i );
			memdump += $"{b:X2} ";
			if ( (i + 1) % 16 == 0 )
			{
				memdump += "\n";
			}
		}
		Log.Info( memdump );
	}

	public void DumpMemoryAsString( uint start, uint length )
	{
		var memdump = "";
		for ( uint i = 0; i < length; i++ )
		{
			byte b = Core.ReadByte( start + i );
			if ( b >= 32 && b <= 126 ) // Printable ASCII range
			{
				memdump += (char)b;
			}
			else
			{
				memdump += ".";
			}
			if ( (i + 1) % 16 == 0 )
			{
				memdump += "\n";
			}
		}
		Log.Info( memdump );
	}

	public string DumpRegisters()
	{
		var dump = new System.Text.StringBuilder();
		dump.AppendLine( "=== Register Values ===" );
		dump.AppendLine( $"EAX: 0x{Core.Registers["eax"]:X8}" );
		dump.AppendLine( $"EBX: 0x{Core.Registers["ebx"]:X8}" );
		dump.AppendLine( $"ECX: 0x{Core.Registers["ecx"]:X8}" );
		dump.AppendLine( $"EDX: 0x{Core.Registers["edx"]:X8}" );
		dump.AppendLine( $"ESI: 0x{Core.Registers["esi"]:X8}" );
		dump.AppendLine( $"EDI: 0x{Core.Registers["edi"]:X8}" );
		dump.AppendLine( $"EBP: 0x{Core.Registers["ebp"]:X8}" );
		dump.AppendLine( $"ESP: 0x{Core.Registers["esp"]:X8}" );
		dump.AppendLine( $"EIP: 0x{Core.Registers["eip"]:X8}" );
		dump.AppendLine( "=== Flags ===" );
		dump.AppendLine( $"ZF: {Core.ZeroFlag}, SF: {Core.SignFlag}, CF: {Core.CarryFlag}, OF: {Core.OverflowFlag}" );
		return dump.ToString();
	}
	public async Task<MessageBoxResult> HaltWithMessageBoxAsync(
		string title,
		string message,
		MessageBoxIcon icon = MessageBoxIcon.Error,
		MessageBoxButtons buttons = MessageBoxButtons.AbortRetryIgnore )
	{
		var result = await MessageBoxUtility.ShowBlocking( message, title, icon, buttons );
		return result;
	}
	public void HaltWithMessageBox( string title, string message, MessageBoxIcon icon = MessageBoxIcon.Error, MessageBoxButtons buttons = MessageBoxButtons.AbortRetryIgnore )
	{
		OnHaltWithMessageBox?.Invoke( title, message, icon, buttons );
		throw new System.Exception( $"!{title}: {message}" );
	}
}
