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
		_apiTable["MessageBoxA"] = core =>
		{
			// Example: pop params, show message, return 1
			uint style = core.Pop();
			uint hwnd = core.Pop();
			uint textPtr = core.Pop();
			uint titlePtr = core.Pop();
			string text = core.ReadString( textPtr );
			string title = core.ReadString( titlePtr );

			var icon = MessageBoxIcon.None;
			var buttons = MessageBoxButtons.OK;
			ParseMessageBoxStyle( style, out icon, out buttons );

			MessageBoxUtility.ShowCustom( text, title, icon, buttons );
			// TODO: Hook to UI
			return 1;
		};
	}
}
