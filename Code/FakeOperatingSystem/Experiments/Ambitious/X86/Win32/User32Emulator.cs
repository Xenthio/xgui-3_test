using FakeDesktop;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.Win32;

public class User32Emulator : APIEmulator
{
	private static void ParseMessageBoxStyle( uint style, out MessageBoxIcon icon, out MessageBoxButtons buttons )
	{
		// Default values
		icon = MessageBoxIcon.None;
		buttons = MessageBoxButtons.OK;

		// Icon flags (see WinUser.h)
		if ( (style & 0x10) != 0 ) icon = MessageBoxIcon.Error;        // MB_ICONHAND/MB_ICONERROR
		else if ( (style & 0x20) != 0 ) icon = MessageBoxIcon.Question; // MB_ICONQUESTION
		else if ( (style & 0x30) != 0 ) icon = MessageBoxIcon.Warning;  // MB_ICONEXCLAMATION/MB_ICONWARNING
		else if ( (style & 0x40) != 0 ) icon = MessageBoxIcon.Information; // MB_ICONASTERISK/MB_ICONINFORMATION

		// Button flags
		switch ( style & 0xF )
		{
			case 0x0: buttons = MessageBoxButtons.OK; break;              // MB_OK
			case 0x1: buttons = MessageBoxButtons.OKCancel; break;        // MB_OKCANCEL
			case 0x2: buttons = MessageBoxButtons.AbortRetryIgnore; break;// MB_ABORTRETRYIGNORE
			case 0x3: buttons = MessageBoxButtons.YesNoCancel; break;     // MB_YESNOCANCEL
			case 0x4: buttons = MessageBoxButtons.OK; break;           // MB_YESNO
			case 0x5: buttons = MessageBoxButtons.RetryCancel; break;     // MB_RETRYCANCEL
																		  // Add more if your MessageBox implementation supports them
			default: buttons = MessageBoxButtons.OK; break;
		}
	}
	public User32Emulator()
	{
		Log.Info( "User32Emulator: Registering MessageBoxA" );

		// Based on the stack dump, this specific executable uses a custom order:
		// HWND, unknown, lpCaption, lpText, style
		_apiTable["MessageBoxA"] = core =>
		{
			// Debug the parameters to help diagnose the issue
			Log.Info( $"MessageBoxA: ESP = 0x{core.Registers["esp"]:X8}" );
			for ( uint i = 0; i < 6; i++ )
			{
				uint param = core.ReadDword( core.Registers["esp"] + (i * 4) );
				Log.Info( $"  Stack[{i * 4}] = 0x{param:X8}" );
			}

			uint hWnd = core.ReadDword( core.Registers["esp"] + 4 );
			uint unknown = core.ReadDword( core.Registers["esp"] + 8 );
			uint textPtr = core.ReadDword( core.Registers["esp"] + 12 );
			uint captionPtr = core.ReadDword( core.Registers["esp"] + 16 );
			uint style = core.ReadDword( core.Registers["esp"] + 20 );

			string text = textPtr != 0 ? core.ReadString( textPtr ) : "";
			string caption = captionPtr != 0 ? core.ReadString( captionPtr ) : "";

			Log.Info( $"MessageBoxA(hWnd=0x{hWnd:X8}, text=\"{text}\", caption=\"{caption}\", style=0x{style:X8})" );

			// Parse style and show the message box
			ParseMessageBoxStyle( style, out var icon, out var buttons );
			MessageBoxUtility.ShowCustom( text, caption, icon, buttons );

			// Clean up stack (stdcall) - there are 5 parameters in this custom call
			core.Registers["esp"] += 20;

			return 1; // IDOK
		};
	}
}
