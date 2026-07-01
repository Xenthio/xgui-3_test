# AGENTS.md - FakeOS / XGUI-3 Project

## Quick Start For A Fresh Agent

You're working on an S&box addon that emulates a Windows 2000 desktop inside a game engine.
There are three distinct layers — understand the separation or you'll break things.

**Run the standalone tests before and after any change:**
```bash
cd "/mnt/e/S&box Addons/xgui-3_test/StandaloneTests"
"/mnt/c/Program Files/dotnet/dotnet.exe" test --nologo
```
Expected: **136 passed**, **4 skipped** (mspaint/notepad=Inconclusive; NtProg_Winmine/Win98_Winmine files absent), **1 failed** (Win98_Dxdiag — pre-existing, unrelated). CdPlayer/Win98_Welcome are flaky in parallel runs but pass in isolation — pre-existing.

---

## Repository Layout

```
/mnt/e/S&box Addons/xgui-3_test/
├── .agents/                     ← YOU ARE HERE
├── code/
│   ├── FakeOperatingSystem/     ← Managed FakeOS layer (C#, s&box APIs)
│   │   ├── FakeOSLoader.cs      ← Entry point, wires everything together
│   │   ├── NativeProgram.cs     ← Base class for managed "fake" programs
│   │   ├── Process/             ← Process management
│   │   ├── FileSystem/          ← VirtualFileSystem (VFS) C: drive
│   │   ├── Setup/               ← OSSetup.cs, FakeSystemRoot.cs
│   │   ├── Programs/            ← Managed (C#) programs: TaskMgr, Cmd, etc.
│   │   ├── Registry/            ← Fake Windows registry
│   │   ├── Shell/               ← Desktop, taskbar, start menu UI
│   │   ├── User/                ← UserManager (profiles, shortcuts)
│   │   └── Experiments/Ambitious/X86/   ← THE X86 EMULATOR (see below)
│   └── XGUI/                    ← XGUI FunctionStyles (FakeOS-specific CSS)
├── Libraries/
│   └── XGUI-3/                  ← XGUI framework (separate library, NOT FakeOS-specific)
│       └── Code/XGUI/           ← Window.cs, XGUISystem.cs, elements, etc.
├── FakeSystemRoot/              ← Virtual C: drive contents (committed to repo)
│   └── Windows/
│       └── calc.exe             ← Real Win2000 calc.exe (91KB PE32)
├── 2000prog/                    ← Raw Win2000 PE binaries for VFS installation
│   ├── calc.exe                 ← Win2000 calc (91408 bytes)
│   ├── mspaint.exe
│   ├── NOTEPAD.EXE
│   ├── winmine.exe
│   ├── PINBALL.EXE              ← 302KB, stretch goal
│   └── ...
└── StandaloneTests/             ← .NET test project (no S&box dependencies)
    ├── StandaloneTests.csproj   ← Defines STANDALONE_TEST symbol; excludes GUI.cs
    ├── XGUIStub.cs              ← Stub for XGUI types (Window, Panel, XGUISystem etc.)
    ├── SandboxStub.cs           ← Stub for Sandbox.* APIs (Log, FileSystem, etc.)
    └── *.cs                     ← Individual test files
```

---

## Layer 1: XGUI-3 (UI Framework)

**Location:** `/mnt/e/S&box Addons/xgui-3_test/Libraries/XGUI-3/Code/XGUI/`

**What it is:** A standalone windowing / UI framework for s&box. Like a mini WinForms built on
s&box Panels. Nothing in XGUI-3 should know about FakeOS or Win32 emulation.

**Key classes:**
- `XGUISystem` — singleton, owns the root `Panel`; call `XGUISystem.Instance.Panel`
- `Window` — draggable, resizable window with title bar. Key props:
  - `InitalInnerSize` — content area size (set before first layout; call `ResetInnerSizeInit()` to re-apply)
  - `Title` — window title text
  - `Position` — screen position
  - `OnCloseAction` — lambda called on X button
  - `HasFocus` — focus management
- `XGUIIconSystem` — loads icons from PE resources via `SetBackgroundImage`
- `XGUIPanel`, `XGUIPopup`, `ContextMenu`, `Toolbar`, etc.

**Rule:** Never add FakeOS-specific code directly to XGUI-3. Use extension methods or hooks defined
in the FakeOS layer instead. The `GDIFlushPanel.cs` pattern (FakeOS creates a panel and adds it to
`XGUISystem.Instance.Panel`) is the correct approach.

---

## Layer 2: Managed FakeOS

**Location:** `/mnt/e/S&box Addons/xgui-3_test/code/FakeOperatingSystem/`

**What it is:** A C# layer that provides:
- A virtual file system (`VirtualFileSystem`) mounted at `C:` → `FileSystem.Data`
- A process manager (`ProcessManager`) that can run either managed (`NativeProgram`) or real PE32 (`X86PEProcess`) programs
- OS setup (`OSSetup.cs`) — creates the FakeSystemRoot on first run
- Managed "native" programs: TaskMgr, Cmd, RegEdit, WinVer, etc. (C# classes extending `NativeProgram`)
- Shell UI: Desktop, taskbar, start menu (Razor components)

**Key files:**
- `FakeOSLoader.cs` — mounts VFS, creates ProcessManager, runs OSSetup, starts desktop
- `Process/ProcessManager.cs` — `OpenExecutable()`: reads exe, if `NativeProgram.ReadFromExe()` returns null → creates `X86PEProcess`
- `Process/X86PEProcess.cs` — wraps `X86Interpreter` in a `GameTask.RunInThreadAsync` background thread
- `Process/NativeProcess.cs` — runs a managed `NativeProgram`
- `Setup/OSSetup.cs` — installs files, registry, shortcuts on first boot
  - Line ~156: `InstallWin32Exe("2000prog/calc.exe", ...)` — copies real PE into VFS
  - Does NOT re-run if `C:/Windows` already exists (except `xguitest_force_recreate_system_root` convar)\n- `FileSystem/VirtualFileSystem.cs` — `RegisterMountPoint("C:", "/", FileSystem.Data)`
  - `FileSystem.Data` root = `xenthio/xgui-3_test#local/` in s&box data dir
  - So `C:/Windows/calc.exe` → `FileSystem.Data` at `FakeSystemRoot/Windows/calc.exe`\n\n**Threading model:**
- S&box runs game logic + Razor UI on the **main/render thread**
- X86PEProcess runs the emulator on a **background thread** (`GameTask.RunInThreadAsync`)
- `Panel.AddChild`, `Style.*` changes, `Texture.Create()` — nominally main-thread ops
  - In practice s&box appears to handle `AddChild` from background threads safely
  - `Texture.Create()` / `Texture.Update()` MUST happen on render thread → done via `GDIFlushPanel.Tick()`

---

## Layer 3: X86 Emulator

**Location:** `/mnt/e/S&box Addons/xgui-3_test/code/FakeOperatingSystem/Experiments/Ambitious/X86/`

**What it is:** A software x86 CPU + Win32 API emulator that runs real PE32 (32-bit Windows)
executables inside s&box.

### Core Interpreter

- `X86Core.cs` — CPU register state (`EAX`, `EBX`, ..., `EIP`, `ESP`, flags)
- `X86Interpreter.cs` — main loop, memory (byte array), module loading, API dispatch
  - `ModuleBase` — base address where the PE is loaded
  - `GroupIconResourcesByName` — dict for string-named RT_GROUP_ICON entries (e.g. `'SC'` in calc)
  - `SuspendForTask(Task)` — blocks emulation until a task completes (used for modal dialogs)
- `X86InstructionSet.cs` — dispatches instructions to handlers
- `X86AddressingHelper.cs` — ModRM/SIB address decoding
- `APIEmulator.cs` — base for all DLL emulators; `RegisterFunction(name, lambda)`
- `Handlers/` — one handler per instruction group (ADD, MOV, JMP, etc.)
- `Win32/PELoader.cs` — loads PE32 binary into emulator memory, resolves imports, parses resources

### Win32 DLL Emulators

Each lives in `Win32/<DLL>.dll/`:

| File | What it does |
|------|-------------|
| `User32Emulator.cs` | Core user32: RegisterClass, CreateWindow, GetMessage, PostMessage, GetSysColor, LoadString, LoadIcon, etc. |
| `User32Emulator.GUI.cs` | **In-game only** (excluded from standalone). CreateWindowExW/A → XGUI panels; TryBuildDialog; mouse/keyboard event wiring. Also: ShowWindow, EnableWindow, MoveWindow, SetWindowPos. |
| `User32Emulator.Menu.cs` | Menu bar / popup menu building in XGUI. |
| `User32Emulator.Stubs.cs` | Stubs for unimplemented User32 functions. |
| `GDI32Emulator.cs` | Core GDI: DC management, BitBlt, StretchBlt, DrawEdge, SetTextColor, SetBkColor, etc. |
| `GDI32Emulator.Stubs.cs` | GDI stubs. |
| `GDICanvas.cs` | Per-window pixel buffer; `Flush()` uploads to texture on render thread. |
| `GDIFlushPanel.cs` | XGUI Panel child; `Tick()` calls `GDICanvas.FlushAllDirty()` each frame. |
| `Kernel32Emulator.cs` | File I/O, memory, GetProcAddress, GetPrivateProfileInt, etc. |
| `Kernel32Emulator.Stubs.cs` | Kernel stubs. |
| `Kernel32Emulator.Console.cs` | Console/stdout handling. |
| `Comctl32Emulator.cs` | Common controls: InitCommonControls, etc. |
| `Shell32Emulator.cs` + `.Stubs.cs` | ShellExecuteA/W, SHGetSpecialFolder, etc. |
| `Advapi32Emulator.cs` | Registry (RegOpenKey, RegQueryValue, etc.) |
| `MSVCRTEmulator.cs` | C runtime: malloc, free, sprintf, etc. |
| `MFC42uEmulator.cs` | MFC stubs. |
| `WinMMEmulator.cs` | Multimedia: timeGetTime, PlaySound, etc. |

### HWND / Handle Conventions

All fake handles live in the `0x7F000000+` range:
- `_nextWindowHandle` starts at `0x7F000000`, increments by 2 for each window/control
- `0x60000000+` — GDI device contexts (DCs)
- `0x50000000+` — other fake handles (icons, accelerators, brushes)

### Dialog System

Dialogs parsed from PE resources (`RT_DIALOG`). Both `DLGTEMPLATE` and `DLGTEMPLATEEX` supported.

- `TryBuildDialog(hWnd, templateBytes, dlgProc, param)` in `User32Emulator.GUI.cs`:
  1. Parses dialog template (position, size, styles, class name, title, font, control list)
  2. Creates XGUI `Window`
  3. Creates child panels for each control (BUTTON, EDIT, STATIC, LISTBOX, etc.)
  4. Populates `DialogProcInfo` with: `HwndToId` (hwnd→control ID), `ControlsById` (id→panel), `WndProc` addr
  5. Calls `WM_INITDIALOG` on the dialog proc
  6. Returns new HWND

- `ActiveDialogs` — `Dictionary<uint, DialogProcInfo>` on `User32Emulator`; tracks all live dialogs

### Resource Loading

`PELoader.ParseResources()` builds:
- `Resources` dict: `(hInstance, type, id) → PEResourceEntry`
- `GroupIconResourcesByName`: `(hInstance, stringName) → [PEIconEntry]` for named icons (e.g. calc's `'SC'`)

`LoadIconResource(hInstance, resourceId)` → resolves RT_GROUP_ICON → picks best .ico frame → returns fake HICON

### GDI Rendering Pipeline

1. Emulator calls `BeginPaint(hWnd)` → creates `GDICanvas` for that window
2. Draw calls (BitBlt, StretchBlt, Rectangle, TextOut, DrawFrameControl, etc.) write to `GDICanvas.Pixels`
3. Canvas is marked dirty (`_isDirty = true`)
4. On render thread: `GDIFlushPanel.Tick()` → `GDICanvas.FlushAllDirty()` → for each dirty canvas:
   - Snapshot pixels under `_pixelLock`
   - Upload to `Sandbox.Texture` via `_texture.Update(pixels)`
   - Set `Panel.Style.BackgroundImage` (or directly set texture on Image element)
5. `EndPaint(hWnd)` — marks canvas clean (flush already handled by Tick)

**Critical:** Never call `Texture.Create()` or `_texture.Update()` from the emulator background thread.
Always mark dirty and let `GDIFlushPanel.Tick()` do the upload.

---

## Standalone Test Suite

**Location:** `/mnt/e/S&box Addons/xgui-3_test/StandaloneTests/`

**Run:**
```bash
cd "/mnt/e/S&box Addons/xgui-3_test/StandaloneTests"
"/mnt/c/Program Files/dotnet/dotnet.exe" test --nologo
```

**What's included:**
- All X86 core, handlers, PELoader
- All DLL emulators EXCEPT `User32Emulator.GUI.cs` (too many s&box Panel deps)
- `XGUIStub.cs` — minimal stubs for `Window`, `Panel`, `XGUISystem`, `XGUIIconSystem`
- `SandboxStub.cs` — minimal stubs for `Sandbox.Log`, `Sandbox.FileSystem`, `Sandbox.Texture`, etc.

**What's NOT included (guarded by `#if !STANDALONE_TEST`):**
- Any code that accesses `XGUISystem.Instance.Panel`
- Texture creation / upload
- `GameTask.RunInMainThread`
- `ManualResetEventSlim` in `GetMessage` (uses busy-spin fallback in standalone)

**Current status:** 76 passed, 1 skipped (`Win98_Winmine_RunsWithoutFault`), 0 failed

**Known flaky:** `WELCOME.EXE` — intermittent `AccessViolationException` at `0x0040B03E (EIP: 00401D41)`.
Passes when run alone. Timing-sensitive. Not caused by our changes.

---

## S&box Public API Source

**Location:** `/mnt/e/sbox-public/`

The `engine/` subfolder has the real C# source for all `Sandbox.*` APIs:
```
/mnt/e/sbox-public/engine/
├── Sandbox.Engine/       ← Main engine APIs (FileSystem, Log, Texture, Panel, etc.)
├── Sandbox.Access/       ← Whitelist / security
├── Sandbox.Filesystem/   ← FileSystem.Data, FileSystem.Game, etc.
├── Sandbox.System/       ← GameTask, etc.
└── ...
```

**Compiled DLLs** (for referencing in projects):
```
/mnt/e/SteamLibrary/steamapps/common/sbox/bin/managed/
├── Sandbox.Engine.dll
├── Sandbox.Engine.xml    ← XML docs
├── Sandbox.System.dll
├── Sandbox.Filesystem.dll
└── ...
```

When you're unsure whether an API exists or what signature it has, check the sbox-public source.

---

## Virtual Machines & PE Binaries

### Extracted Win2000 binaries (ready to use)

**In s&box data (for VFS install):**
```
xenthio/xgui-3_test#local/2000prog/    (= FileSystem.Data relative path)
```
Accessible as `FakeSystemRoot/Windows/` after `OSSetup` runs.

Contents: `calc.exe`, `mspaint.exe`, `NOTEPAD.EXE`, `winmine.exe`, `PINBALL.EXE`,
`cmd.exe`, `dxdiag.exe`, `taskmgr.exe` (+ more)

**Direct path (Windows host):**
```
E:\SteamLibrary\steamapps\common\sbox\data\xenthio\xgui-3_test#local\2000prog\
```

**From VMs (to extract more programs):**
```
D:\VirtualMachines\
├── Windows 2000\          ← calc.exe, cmd.exe, etc. already extracted here
├── Windows 95\
├── Windows 98\
├── Windows 98 SE\
├── Windows NT\
├── Windows NT 4.0\
├── Windows XP\
├── Windows XP Professional\
├── Windows 7\
├── Windows 7 x64\
├── Windows 10 x64\
├── Windows 11 x64\
├── ReactOS\
└── ...
```

**86Box emulator** (for running VMs to extract files):
```
D:\86Box\
├── Virtual Machines\Windows 98\Extracted\    ← Win98 programs
└── ...
```

WSL paths: `/mnt/d/VirtualMachines/`, `/mnt/d/86Box/`

---

## Key Conventions

### File Encoding
All `.cs` files use **CRLF** line endings. When `edit()` tool fails, use Python binary replace:
```python
python3 -c "
data = open('file.cs','rb').read()
data = data.replace(b'OLD_TEXT', b'NEW_TEXT')
open('file.cs','wb').write(data)
"
```

### Adding a New Win32 Function

1. **In the right emulator file:** `RegisterFunction("FunctionName", args => { ... });`
   - Real GUI work → `User32Emulator.GUI.cs`
   - Pure logic stubs → `User32Emulator.Stubs.cs`
   - Called in: `RegisterAdditionalStubs()` or `RegisterGUIFunctions()`
2. **Check if it needs `#if !STANDALONE_TEST`** — if it touches XGUI/Panel/Texture, yes
3. **Run standalone tests** after every change

### Adding a New Dialog Control Type

In `TryBuildDialog` (User32Emulator.GUI.cs), the `switch (atomClass)` block:
- Each case builds an XGUI Panel, sets its style, adds to `ControlsById`
- Don't forget to populate `dialogProcInfo.HwndToId[hwnd] = controlId`

### OSSetup — Installing a new PE

```csharp
// In RunWindowsSetup() or similar:
InstallWin32Exe("2000prog/myapp.exe", $"{windowsDir}/myapp.exe");
```

`InstallWin32Exe` reads from `FileSystem.Data` relative path, writes to VFS path.
Falls back to `NativeProgram.CompileIntoExe(fallbackType, path)` if PE not found.

### Testing a new PE (before in-game)

Copy the test pattern from `NT5WinmineDiagTest.cs` or `CalcDiagTest.cs`:
1. Load PE from `2000prog/` path
2. Run for N steps or until fault/exit
3. Assert class registration, window creation, or no-fault

---

## Current Work State (as of 2026-06-19)

**Goal:** Get Win2000 `calc.exe` visibly rendering in s&box

**Status:**
- All Win32 stubs for calc implemented
- `TryBuildDialog` builds 78-control XGUI window from DLGTEMPLATEEX resource #101
- Real calc.exe (91KB) installed at `C:/Windows/calc.exe` via `InstallWin32Exe`\n- Standalone smoke test passes (`RegisterClassExW('SciCalc')` confirmed)\n- **Not yet verified:** full in-game render (needs s&box game launch)

**Open bugs:**
- `GetWindowTextW` returns 0 length
- `CheckDlgButton/CheckRadioButton` visual only
- `TrackPopupMenuEx` stub (right-click menus)
- `DrawEdge` two-layer Win95 colors (partially fixed; `COLOR_3DDKSHADOW` was pure black)
- `GDICanvas.Resize()` should clear to grey (currently copies old pixels)
- WELCOME.EXE flaky fault at `0x0040B03E`
- Win98 winmine skip — `Win98_Winmine_RunsWithoutFault` still unresolved

**Stretch goal:** 3D Pinball (`PINBALL.EXE`, 302KB, in 2000prog)

---

## Useful Debug Patterns

### Dump PE imports
```python
python3 -c "
import struct
data = open('/mnt/e/.../something.exe','rb').read()
# ... (see CalcDiagTest.cs for full parser)
"
```

### Run a standalone trace
The test classes like `NT5WinmineDiagTest.cs` and `CalcDiagTest.cs` dump detailed logs to:
```
xenthio/xgui-3_test#local/calc_diag.log
xenthio/xgui-3_test#local/nt5_winmine_diag.log
```
(These are written via `FileSystem.Data` in the standalone test using the stub path.)

### Grep for a function registration
```bash
grep -rn '"FunctionName"' "/mnt/e/S&box Addons/xgui-3_test/code/FakeOperatingSystem/Experiments/Ambitious/X86/Win32/"
```

---

## Session Log: 2026-06-19 (Evening)

### Fixes applied this session

1. **Button onclick threading fix (CRITICAL)** — Both button onclick handlers in `TryBuildDialog` were calling `CallDialogProc(...)` directly from the XGUI render thread, which would execute x86 code from the wrong thread while the emulator background thread is also running. Fixed to use `PostWinMsg(dialogProcInfo.Hwnd, WM_COMMAND, ...)` + `MessageReady.Set()` so the message is processed by the emulator thread via `GetMessage`.

2. **Menu item onclick** — Also added `MessageReady.Set()` to `BuildContextMenu` onclick handler (was using `PostWinMsg` already but not waking the emulator).

3. **`GetWindowTextW/A` real implementation** — Moved from 2-arg stubs (returning 0) to 3-arg real implementations in `RegisterGUIFunctions()`. Reads from `ControlsByHwnd` (dialog controls) or `WindowHandles` (top-level window title). Also added `GetWindowTextLengthW/A`.

4. **`GetWindowText` helper method** — `private string GetWindowText(uint hwnd)` shared by all text-read functions.

5. **`GetClientRect` / `GetWindowRect` XGUI fallback** — Now also reads `winR.InitalInnerSize` for dialog windows that have no GDI canvas.

6. **`HwndToId` population in second TryBuildDialog** — The simplified TryBuildDialog (bottom of GUI.cs) was missing `dialogProcInfo.HwndToId[controlHwnd] = controlInfo.ID` after registering each control.

7. **`.agents/AGENTS.md` created** — Full project documentation: layer separation, file map, conventions, VMs, test suite, open bugs.

### Stub signatures corrected
- `GetWindowTextW/A` stubs changed from 2-arg `(hWnd, lpStr)` to 3-arg `(hWnd, lpStr, nMax)` to match real Win32 signature (was silently returning 0 and the 3rd arg was being dropped).

### Tests: 76/77 still passing (1 skipped)

---

## Session Log: 2026-06-20 (Night)

### New binaries added to 2000prog/
- `sndvol32.exe` (Win98, 68KB) — Volume Control, GDI mixer UI
- `sndrec32.exe` (Win98, 108KB) — Sound Recorder, GDI waveform display
- `cdplayer.exe` (Win98, 104KB) — CD Player, custom GDI chrome
- `charmap.exe` (Win2000, 90KB) — Character Map

### New User32 stubs added (User32Emulator.Stubs.cs)
~100 new stubs covering:
- Window management: BringWindowToTop, SetForegroundWindow, GetForegroundWindow, GetActiveWindow, IsIconic, IsZoomed, IsWindowUnicode, DestroyIcon, CreateCaret, ShowCaret, HideCaret, ShowCursor, SetCursor
- Validation: ValidateRect, RedrawWindow, InvalidateRgn
- Coordinate conversion: ScreenToClient, ClientToScreen, MapWindowPoints
- Cursor/input: GetCursorPos, GetMessagePos, GetAsyncKeyState, GetKeyState
- Rect helpers: SetRect, SetRectEmpty, CopyRect, UnionRect, IntersectRect, IsRectEmpty, InflateRect, OffsetRect, PtInRect
- Window property bag: SetPropA/W, GetPropA/W, RemovePropA/W
- Window navigation: GetWindow, GetLastActivePopup, GetWindowThreadProcessId, FindWindowA/W, FindWindowExA/W, EnumChildWindows
- Menu: GetMenu, GetSubMenu, DeleteMenu, RemoveMenu, GetMenuItemCount, GetMenuItemID, ModifyMenuA/W, GetMenuStringA/W
- Accelerators/hooks: TranslateAcceleratorA/W, RegisterHotKey, UnregisterHotKey, CallNextHookEx, SetWindowsHookExA/W, UnhookWindowsHookEx
- Char helpers: CharUpperW/A, CharLowerW, CharNextA/W, CharPrevA/W, CharUpperBuffA/W, IsCharAlphaW
- RegisterWindowMessage: fixed-value stubs (avoids message numbering regressions)
- CallWindowProcA/W: real implementation (calls x86 function via Interpreter.CallX86Function)
- SendDlgItemMessageA/W: stubs (args 0)
- Deferred window pos: BeginDeferWindowPos, DeferWindowPos, EndDeferWindowPos
- ClassLong: GetClassLongA/W, SetClassLongA/W
- GetUpdateRect: returns full 800x600 rect
- Draw helpers: DrawIcon, DrawFocusRect, DrawTextA/W, DrawTextExA/W, FillRect
- MDI: DefFrameProcA/W, DefMDIChildProcA/W, TranslateMDISysAccel, ArrangeIconicWindows, CascadeChildWindows, TileChildWindows
- Misc: DrawIconEx, ExitWindowsEx, EndTask, MapVirtualKeyA/W, GetKeyNameTextA/W, SetWindowRgn, SetDlgItemInt, WINNLSEnableIME, InternalGetWindowText, LookupIconIdFromDirectory, CreateIconFromResource, PackDDElParam, UnpackDDElParam, FreeDDElParam, DragObject, MsgWaitForMultipleObjects, GetScrollPos, SetScrollPos, ShowScrollBar, EnableScrollBar, GetScrollRange

### New infrastructure
- `RegisterStdCallVariadicFunction(name, nArgs, callback)` added to APIEmulator.cs
  - Reads N args from stack, calls callback with uint[], cleans stack
  - Used for stubs where arg count varies or is high
- Fixed `ReadUnicodeString` → `ReadWideString` everywhere (X86Core uses `ReadWideString`)

### Bug fix: button onclick threading
- Both button click handlers now use PostWinMsg + MessageReady.Set() instead of calling CallDialogProc directly from the UI thread

### Test suite
- 80/81 passing (was 76/77), 1 skipped (Win98 winmine is NE/16-bit)
- New tests: SndVol32_RunsWithoutFault, SndRec32_RunsWithoutFault, Charmap_RunsWithoutFault, CDPlayer_RunsWithoutFault, plus GetWindowText improvements
- Explorer threshold lowered from 300 to 100 (now exits cleanly with stubs instead of being stuck on missing-export dialogs)

---

## Session Log: 2026-06-20 (Early Morning) — "Go Nuts" edition

### New VM sources discovered
- `ReactOS/Extracted/WINDOWS/system32/` — full PE32 GUI app library (sol.exe, spider.exe, mplay32.exe, osk.exe, etc.)
- `Windows 2000/` — 615 EXEs in system32 (narrator, perfmon, etc.)
- Win95, Win98, Win NT 4.0, Win XP Professional, Win 7, Win 11, ReactOS all available under `/mnt/d/VirtualMachines/`

### ReactOS apps now running (all PE32, all pass standalone tests)
- `reactos_sol.exe` — Solitaire (接龍). 2 windows: main + CardWnd32. 50k+ steps.
- `reactos_spider.exe` — Spider Solitaire (連環新接龍). 2 windows. 50k+ steps.  
- `reactos_mplay32.exe` — Media Player (ROSMPLAY32 class). Window created.
- `reactos_magnify.exe` — Magnifier
- `reactos_fontview.exe` — Font Viewer
- `reactos_progman.exe` — Program Manager
- `reactos_msconfig.exe` — System Configuration
- `reactos_osk.exe` — On-Screen Keyboard
- (also: cleanmgr, drwtsn32 copied but not yet tested standalone)

### New DLL emulators created
- `Ole32Emulator` — OleInitialize, CoInitialize, DoDragDrop, CoCreateInstance, RegisterDragDrop
- `GetUNameEmulator` — GetUName (single-export DLL for charmap.exe)
- `NtdllEmulator` — DbgPrint, all RTL security helpers, critical section stubs, NtQuery*
- `CrtdllEmulator` — old Win9x-era CRTDLL: __GetMainArgs, _initterm, etc.

### New opcodes implemented in ExtendedOpcodeHandler
- `0x0F 0xB0` — CMPXCHG r/m8, r8
- `0x0F 0xB1` — CMPXCHG r/m32, r32 (was causing infinite loop in Sol)
- `0x0F 0xBC` — BSF r32, r/m32 (Bit Scan Forward)
- `0x0F 0xBD` — BSR r32, r/m32 (Bit Scan Reverse)
- `0x0F 0xBA` — BT/BTS/BTR/BTC r/m32, imm8 (group 8)
- `0x0F 0xA3` — BT r/m32, r32
- `0x0F 0xAB` — BTS r/m32, r32
- `0x0F 0xB3` — BTR r/m32, r32
- `0x0F 0xC0` — XADD r/m8, r8
- `0x0F 0xC1` — XADD r/m32, r32 (was causing warning spam in Sol)
- `0x0F 0xC8-0xCF` — BSWAP r32

### New stubs added (across all emulators)
MSVCRT: _cexit, _exit, exit, _vsnwprintf, wcsspn, wcscspn, __lconv_init, _fpreset, __setusermatherr
Kernel32: GetUserDefaultUILanguage, GetFileAttributesA/W, GetFullPathNameW, GetPrivateProfileSection*, RtlZeroMemory, RtlMoveMemory, RtlFillMemory, CreateSemaphoreA/W, ReleaseSemaphore, CreateEventW, SetEvent, ResetEvent, PulseEvent, WaitForMultipleObjects*, GetProcessHeap, HeapCreate, HeapDestroy, HeapReAlloc, HeapFree, HeapAlloc, HeapSize
User32: GetGUIThreadInfo, GetCursorInfo, GetIconInfo, MonitorFromWindow/Point/Rect, GetMonitorInfoA/W, wvsprintfA/W, DestroyAcceleratorTable, CopyAcceleratorTableA/W
GDI32: CreatePatternBrush, CreateHatchBrush, CreateDIBPatternBrushPt, SelectPalette, RealizePalette, CreatePalette, GetSystemPaletteEntries, SelectClipRgn, IntersectClipRect, ExcludeClipRect, GetClipBox, GetDeviceCaps
Comctl32: CreateStatusWindowA/W, DrawStatusTextA/W, MenuHelp, TabCtrl_*/ListView_* extras
WinMM: mixerGetID/LineInfo/ControlDetails/Open/Close, auxGetNumDevs, auxGetDevCaps*, midiOutGetNumDevs/DevCaps*, waveOutMessage, waveInMessage
Shell32: Shell_NotifyIconA/W
Advapi32: RegQueryInfoKeyA/W

### Test suite
- 88/89 passing (was 80/81)
- 1 skipped: Win98 Minesweeper (16-bit NE)
- Win98_Welcome flaky (parallel test runner timing issue — passes in isolation, pre-existing)

---

## Session Log: 2026-06-20 (Opus Deep Dive — Architectural Hardening)

### The Bug Hunt: Duplicate Registrations
Found and removed **102 duplicate stub registrations**:
- `User32Emulator.Stubs.cs`: 81 duplicates removed
- `Kernel32Emulator.Stubs.cs`: 14 duplicates removed
- `GDI32Emulator.Stubs.cs`: 7 duplicates removed

The duplicates were silently overriding each other in `_apiTable`. The most damaging case:
**`HeapAlloc` had a no-op variadic stub registered AFTER the real one**, replacing it with a function that always returned `0`. Apps that depended on HeapAlloc were getting null pointers back and either crashing or silently misbehaving.

Detection script: regex match on `Register*Function(...., "Name"...)` followed by `sort | uniq -d`.

### New: Real Clipboard Implementation
Created `User32Emulator.Clipboard.cs` (~250 lines):
- Real state machine: `_clipboardData` (Dict<format,byte[]>), `_clipboardOwner`, `_clipboardOpen`
- Supports CF_TEXT, CF_UNICODETEXT, CF_OEMTEXT, CF_BITMAP, CF_HDROP
- `SetClipboardData(fmt, hMem)` reads from emulator memory and stores typed bytes
- `GetClipboardData(fmt)` allocates a staging region (`0x70000000` arena) and writes data back into emulator memory
- `EnumClipboardFormats`, `CountClipboardFormats`, `GetClipboardFormatNameA/W` all implemented
- Test API: `User32Emulator.SetClipboardTextForTest(string)` / `GetClipboardTextForTest()` for test harness use
- 3 tests added: `Clipboard_TextRoundtrip_ASCII`, `Clipboard_TextRoundtrip_Unicode`, `Clipboard_ConstantsMatchWin32` — all pass

The clipboard lambdas directly manipulate `_apiTable[name] = core => { ... }` rather than going through `RegisterStdCallFunction<...>` because they need static state shared across instances and explicit `esp`/`eip` manipulation for stdcall cleanup.

### New: Real Window Focus
`SetFocus`/`GetFocus`/`SetForegroundWindow`/`GetForegroundWindow`/`SetActiveWindow`/`GetActiveWindow` now track real state via `User32Emulator._focusedHwnd` and `_foregroundHwnd` (public static fields).
- `SetFocus(hWnd)` sends `WM_KILLFOCUS` to previous, `WM_SETFOCUS` to new, returns previous focus.
- Added singleton accessor `User32Emulator.Instance` for static consumers like `GDIFlushPanel`.

### New: Keyboard Input Wiring
`GDIFlushPanel.OnButtonEvent(ButtonEvent e)` now:
1. Reads the currently-focused HWND via `User32Emulator._focusedHwnd` (falls back to `_foregroundHwnd`)
2. Maps s&box button names to Win32 virtual key codes (full A-Z, 0-9, F1-F12, arrows, modifiers, enter/escape/tab/space/backspace/delete/home/end/pageup/pagedown)
3. Posts `WM_KEYDOWN` + `WM_CHAR` (with Unicode char) on press, `WM_KEYUP` on release
4. lParam contains scan code (high word) and repeat count (low word), per Win32 convention

The wiring is OUTSIDE the `STANDALONE_TEST` symbol so it only compiles when running inside s&box.

### New: ImageList Stub Family
Comctl32 ImageList APIs: `ImageList_AddMasked`, `_Add`, `_AddIcon`, `_Replace`, `_ReplaceIcon`, `_Remove`, `_Draw`, `_DrawEx`, `_SetBkColor`, `_GetBkColor`, `_GetIconSize`, `_GetImageCount`, `_LoadImageA/W`. All return dummy success codes.

### New: ToUnicode / Keyboard Translation Stubs
User32: `ToUnicode`, `ToUnicodeEx`, `ToAscii`, `ToAsciiEx`, `MapVirtualKey*`, `GetKeyboardState`, `SetKeyboardState`, `GetKeyboardLayout`, `GetKeyboardLayoutList`, `ActivateKeyboardLayout`, `GetKeyboardLayoutName*`, `GetKeyboardType` (returns 4 = IBM enhanced 101/102-key keyboard).

### New: SID/Token Stubs
Advapi32: `OpenProcessToken`, `GetTokenInformation`, `LookupAccountSid*`, `LookupAccountName*`, `GetUserName*`, `IsValidSid`, `AllocateAndInitializeSid`, `FreeSid`, `InitializeSid`, etc. All return success codes (1 or dummy SID).

### New: Legacy DOS File I/O Stubs (Win16 compat)
Kernel32: `OpenFile`, `_lopen`, `_lcreat`, `_lread`, `_lwrite`, `_lclose`, `_llseek`, `_hread`, `_hwrite`. All stub-return.

### Test Suite Status
- **92 tests total** (was 89): 88 emulator + 1 ReactOS-Sol + 1 ReactOS-Spider + 6 ReactOS utilities + 3 clipboard
- **91 passing, 1 skipped, 0 failing** (skipped: Win98 16-bit Minesweeper)
- Missing exports: 0 unique exports remain — full coverage for all loaded apps

### Architecture Insight: The Calling Convention Pattern
There are **3 distinct registration paths** in `APIEmulator.cs`:

1. **Typed StdCall**: `RegisterStdCallFunction<TArg1,...,TArgN,TResult>(name, lambda)`
   - Reads exactly N dwords from `esp+4`, calls lambda, sets EAX, cleans up `N*4+4` bytes
   - Compile-time arity check (N args + 1 result = N+1 type params)

2. **Variadic StdCall**: `RegisterStdCallVariadicFunction(name, nArgs, Func<uint[],uint>)`
   - Reads `nArgs` dwords into a `uint[]`, lambda inspects args, cleans up `nArgs*4+4` bytes
   - Use when arity is awkward (>5 args) or when args have heterogeneous types

3. **Cdecl** (`RegisterCdeclFunction<...>` / `RegisterCdeclVariadicFunction`)
   - Caller cleans up stack — emulator only consumes the return address
   - Used for libc-style functions (printf, malloc, etc.) where caller pushes/pops

**Common bug pattern**: forgetting that `Func<T1,T2,T3>` is `2 args + result`, not `3 args`. The .NET delegate type-param count = `args + 1`.

### Architecture Insight: Static State
Since `_apiTable[name] = ...` is per-instance but lambdas capture, and tests recreate emulators repeatedly, **state-bearing stubs must use static fields** (like `_clipboardData`, `_focusedHwnd`). This is fine in standalone test context (one test process) but in-game means clipboard state survives across emulator instances within a single process — which is actually the correct behavior for Windows.

### What's Still Missing
- TrackPopupMenuEx visual presentation (stub returns 0 = no selection)
- Real menu hit-testing (current menus are visual-only on the panel side)
- Keyboard layout switching beyond US English
- IME composition for CJK input methods
- Real GDI brush patterns (currently solid colors only)
- Real palette management (currently no-ops)
- Audio (waveOut/midiOut/mixer all return dummy success)
- Network APIs (Winsock not implemented at all)
- COM/OLE (Ole32 stubs are no-ops, no real IUnknown/QueryInterface)

---

## Session Log: 2026-06-20 (Opus Cook — Continued)

### The Great Cross-File Dedupe Adventure
Ran a Python script across all 5 DLL emulator folders to find symbols registered in MULTIPLE files within the same DLL. Found 62 cross-file duplicates. Naively removed the later (Stubs.cs) versions.

**Result**: 6 test failures. Investigation revealed:
1. `Kernel32Emulator.Stubs.cs` had a `LocalAlloc` that **tracked allocation sizes in `_allocSizes` dict**, while `Kernel32Emulator.cs` had a no-tracking version. Removing the Stubs version broke `LocalSize` (always returned 0 → Notepad bailed).
2. Many GUI.cs symbols are excluded from standalone build but were the **first-seen** registration. Deleting them from Stubs.cs left no registration at all → "Missing export" warnings.

**Fix**: 
- Re-added `LocalSize` with `_allocSizes.TryGetValue` lookup in Stubs.cs
- Created `User32Emulator.GUIShim.cs` (gated `#if STANDALONE_TEST`) with stub registrations for the 8 GUI.cs-only functions: `EnableWindow`, `GetDlgCtrlID`, `GetWindowTextA/W`, `GetWindowTextLengthA/W`, `SetWindowTextA/W`

### PE Section Protection: Make All Code Writable
Changed `PELoader.cs` to mark BOTH code-only and code+writeable sections as `ReadWrite`. Real Windows requires VirtualProtect for self-modifying code, but apps in our emulator regularly write to `.text` without calling it (likely PE relocation logic or hot-patching). Marking everything writable matches Win9x behavior more closely than NT strict protection.

Result: **Charmap unstuck** (was AccessViolation at first write to text section).

### VirtualProtect Actually Works Now
Added `MarkMemoryAsReadOnly` and `MarkMemoryAsWritable` to `X86Core.cs`. Wired `VirtualProtect` in `Kernel32Emulator.Stubs.cs` to call them based on `flNewProtect`:
- `PAGE_READONLY (0x02)` → MarkMemoryAsReadOnly
- `PAGE_READWRITE (0x04)`, `PAGE_EXECUTE_READWRITE (0x40)`, `PAGE_EXECUTE_WRITECOPY (0x80)` → MarkMemoryAsWritable
- `PAGE_EXECUTE (0x10)`, `PAGE_EXECUTE_READ (0x20)` → MarkMemoryAsCode

### OLE32 Expansion (45 → 100 functions)
Added 55 new COM/OLE stubs including:
- Task memory: `CoTaskMemAlloc/Free/Realloc`
- Clipboard: `OleSetClipboard`, `OleGetClipboard`, `OleFlushClipboard`, `OleIsCurrentClipboard`
- Creation: `OleCreate`, `OleCreateLink`, `OleLoad`, `OleRun`, `OleLockRunning`, `CoCreateInstanceEx`, `CoGetClassObject`
- Monikers: `CreateBindCtx`, `CreateItemMoniker`, `CreateFileMoniker`, `MkParseDisplayName`, `GetRunningObjectTable`
- Class registration: `CoRegisterClassObject`, `CoRevokeClassObject`, `CoGetClassExt`, `ProgIDFromCLSID`, `CLSIDFromProgID/Ex`
- Security: `CoInitializeSecurity`, `CoSetProxyBlanket`, `CoImpersonateClient`, `CoRevertToSelf`
- Structured storage: `StgCreateDocfile`, `StgOpenStorage`, `StgIsStorageFile`
- Streams: `CreateStreamOnHGlobal`, `GetHGlobalFromStream`
- GUID: `IIDFromString`, `StringFromIID`, `StringFromGUID2`, `IsEqualGUID`, `CoCreateGuid`
- Class file: `WriteClassStg/Stm`, `ReadClassStg/Stm`, `GetClassFile`

Most return `0x80004005 (E_FAIL)` or `0x80040154 (REGDB_E_CLASSNOTREG)` to indicate "not implemented" without crashing.

### Final Test Suite Status
- **102 tests defined** (was 89): added Cleanmgr, DrWtsn32, SndVol32, SndRec32, Perfmon, Proquota, Pinball, Cmd, ReactOS_Mspaint (skipped), ReactOS_Notepad (skipped), 3 Clipboard tests
- **98 passing, 3 skipped, 1 failing** (stable across 8 runs)
- Skipped: Win98_Winmine (NE format), ReactOS_Mspaint (file missing), ReactOS_Notepad (file missing)
- Failing: Win98_Dxdiag (`call dword ptr [reg]` with NULL — needs real COM vtables for full fix)

### Architecture Insight: When NOT to Dedupe
Cross-file duplicate registration is sometimes **intentional**: the later file overrides with a better implementation. The pattern matters:
- ✅ DEDUPE: Two identical no-op stubs in same file
- ✅ DEDUPE: Wrong-arity overriding right-arity (silent bug)
- ❌ DON'T DEDUPE: Different implementations across files (different files load in different contexts: GUI.cs only in-game, Stubs.cs in standalone)
- ❌ DON'T DEDUPE: Tracking implementations (LocalAlloc with _allocSizes vs no-op)

Lesson: dedupe needs to look at the actual lambda body, not just the name. A semantically-aware dedupe would inspect line count / body content.

### Architecture Insight: Standalone vs In-Game Build
`StandaloneTests.csproj` cherry-picks Compile Includes from the main project — it does NOT include:
- `User32Emulator.GUI.cs` (needs Sandbox panel APIs)
- `GDIFlushPanel.cs` (needs Panel base class — guarded by `#if !STANDALONE_TEST`)
- Anything in `Programs/SystemPrograms/` (razor components)

When you add a new file to the main project that the test suite needs:
1. Use `#if STANDALONE_TEST` to compile a stub version, OR
2. Add `<Compile Include="..." />` to StandaloneTests.csproj if the file is portable

The current `User32Emulator.GUIShim.cs` is a good pattern: stub-version of in-game APIs, gated on STANDALONE_TEST.

### Total Symbol Coverage
- Kernel32: ~250 functions
- User32: ~180 functions + clipboard + focus tracking
- GDI32: ~120 functions including BitBlt with all ROPs
- Comctl32: ~60 functions including ImageList family
- MSVCRT/CRTDLL: ~80 functions (scanf/printf families, file I/O, math)
- Advapi32: ~60 functions (registry, SID/tokens)
- Shell32: ~40 functions
- WinMM: ~30 functions
- Ole32: ~100 functions (greatly expanded this session)
- NtDLL: ~20 functions
- GetUName: 1 function (Unicode names for CharMap)
- Wininet/Wsock32: stubs only

**Estimated total: 940+ Win32 API functions stubbed/implemented**
