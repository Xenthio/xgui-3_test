using FakeDesktop;
using System;

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
			case 0x4: buttons = MessageBoxButtons.YesNo; break;           // MB_YESNO - Fixed from OK to YesNo
			case 0x5: buttons = MessageBoxButtons.RetryCancel; break;     // MB_RETRYCANCEL
			default: buttons = MessageBoxButtons.OK; break;
		}
	}
	public User32Emulator()
	{
		Log.Info( "User32Emulator: Registering MessageBoxA" );

		_apiTable["MessageBoxA"] = core =>
		{
			uint returnAddress = core.ReadDword( core.Registers["esp"] );
			uint hWnd = core.ReadDword( core.Registers["esp"] + 4 );
			uint textPtr = core.ReadDword( core.Registers["esp"] + 8 );
			uint captionPtr = core.ReadDword( core.Registers["esp"] + 12 );
			uint style = core.ReadDword( core.Registers["esp"] + 16 );

			Log.Info( $"MessageBoxA called with hWnd=0x{hWnd:X8}, lpText=0x{textPtr:X8}, lpCaption=0x{captionPtr:X8}, uType=0x{style:X8}" );

			string text = textPtr != 0 ? core.ReadString( textPtr ) : "(null)";
			string caption = captionPtr != 0 ? core.ReadString( captionPtr ) : "(null)";

			Log.Info( $"MessageBoxA(hWnd=0x{hWnd:X8}, lpText=\"{text}\", lpCaption=\"{caption}\", uType=0x{style:X8})" );

			ParseMessageBoxStyle( style, out var icon, out var buttons );
			MessageBoxUtility.ShowCustom( text, caption, icon, buttons );

			// Set return value in EAX (IDOK)
			core.Registers["eax"] = 1;

			// Adjust stack for stdcall: 4 params + return address
			core.Registers["esp"] += 20;

			// Set EIP to return address
			core.Registers["eip"] = returnAddress;

			Log.Info( $"MessageBoxA returning to 0x{returnAddress:X8}" );

			return 1; // Still return value for completeness, but EAX is what matters
		};



		// wsprintfA - Formats a string using variable arguments
		_apiTable["wsprintfA"] = core =>
		{
			// Get the return address
			uint returnAddress = core.ReadDword( core.Registers["esp"] );

			// Extract parameters - note that this is a varargs function
			uint bufferPtr = core.ReadDword( core.Registers["esp"] + 4 );
			uint formatPtr = core.ReadDword( core.Registers["esp"] + 8 );

			Log.Info( $"wsprintfA called with bufferPtr=0x{bufferPtr:X8}, formatPtr=0x{formatPtr:X8}" );

			// Read the format string
			string format = core.ReadString( formatPtr );
			Log.Info( $"wsprintfA format: \"{format}\"" );

			// We'll implement basic formatting for %d, %s, %x
			string result = "";
			int paramIndex = 3; // Start at the 3rd parameter (args begin at esp+12)

			for ( int i = 0; i < format.Length; i++ )
			{
				if ( format[i] == '%' && i + 1 < format.Length )
				{
					char specifier = format[i + 1];
					i++; // Skip the specifier

					switch ( specifier )
					{
						case 'd': // Integer
							{
								uint value = core.ReadDword( core.Registers["esp"] + (uint)(paramIndex * 4) );
								result += ((int)value).ToString();
								paramIndex++;
								break;
							}
						case 'u': // Unsigned integer
							{
								uint value = core.ReadDword( core.Registers["esp"] + (uint)(paramIndex * 4) );
								result += value.ToString();
								paramIndex++;
								break;
							}
						case 'x': // Hex
							{
								uint value = core.ReadDword( core.Registers["esp"] + (uint)(paramIndex * 4) );
								result += value.ToString( "x" );
								paramIndex++;
								break;
							}
						case 'X': // Uppercase hex
							{
								uint value = core.ReadDword( core.Registers["esp"] + (uint)(paramIndex * 4) );
								result += value.ToString( "X" );
								paramIndex++;
								break;
							}
						case 's': // String
							{
								uint strPtr = core.ReadDword( core.Registers["esp"] + (uint)(paramIndex * 4) );
								string str = core.ReadString( strPtr );
								result += str;
								paramIndex++;
								break;
							}
						case '%': // Escaped percent
							result += '%';
							break;
						default:
							// Unknown format specifier, just append it as-is
							result += "%" + specifier;
							break;
					}
				}
				else
				{
					result += format[i];
				}
			}

			Log.Info( $"wsprintfA result: \"{result}\"" );

			// Safety check for stack buffer (check if buffer is on stack)
			bool isStackBuffer = bufferPtr >= 0x00070000 && bufferPtr < 0x00080000;
			Log.Info( $"Buffer location: {(isStackBuffer ? "STACK" : "HEAP/OTHER")} at 0x{bufferPtr:X8}" );

			// Write the formatted string to the buffer (including null terminator)
			if ( result.Length > 255 && isStackBuffer )
			{
				Log.Warning( $"DANGER: wsprintfA result too long for stack buffer: {result.Length} chars" );
				// Truncate to avoid stack overflow
				result = result.Substring( 0, 255 );
			}

			try
			{
				// Verify memory before writing
				byte testByte = core.ReadByte( bufferPtr );
				Log.Info( $"Memory at buffer address is accessible: 0x{testByte:X2}" );
			}
			catch ( Exception ex )
			{
				Log.Error( $"ERROR: Buffer memory not accessible: {ex.Message}" );
			}

			// Write the string and log every 10 bytes for debugging
			for ( int i = 0; i <= result.Length; i++ )
			{
				byte b = i < result.Length ? (byte)result[i] : (byte)0;
				core.WriteByte( bufferPtr + (uint)i, b );

				if ( i % 10 == 0 )
				{
					Log.Info( $"Wrote to 0x{bufferPtr + (uint)i:X8}: {(i < result.Length ? result[i] : '\0')}" );
				}
			}

			// Dump a bit of memory after writing to verify
			Log.Info( "Memory after writing (first 20 bytes):" );
			for ( int i = 0; i < 20 && i <= result.Length; i++ )
			{
				byte b = core.ReadByte( bufferPtr + (uint)i );
				Log.Info( $"Offset +{i}: 0x{b:X2} '{(b >= 32 && b < 127 ? (char)b : '.')}'" );
			}

			// Set return value in EAX
			uint returnValue = (uint)result.Length;
			core.Registers["eax"] = returnValue;

			// In cdecl, caller cleans up the stack, so we don't adjust ESP
			// But we do need to set EIP to the return address
			core.Registers["eip"] = returnAddress;

			Log.Info( $"wsprintfA returning length {returnValue} to 0x{returnAddress:X8}" );

			return returnValue;
		};
	}
}
