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

		RegisterStdCallFunction<uint, string, string, uint, uint>(
			"MessageBoxA",
			( hWnd, text, caption, style ) =>
			{
				Log.Info( $"MessageBoxA(hWnd=0x{hWnd:X8}, lpText=\"{text}\", lpCaption=\"{caption}\", uType=0x{style:X8})" );

				ParseMessageBoxStyle( style, out var icon, out var buttons );
				MessageBoxUtility.ShowCustom( text, caption, icon, buttons );

				return 1; // IDOK
			}
		);

		RegisterStdCallFunction<uint, uint, uint, uint, uint>(
			"LoadStringA",
			( hInstance, uID, lpBuffer, cchBufferMax ) =>
			{
				// Try to find the string resource
				if ( !Interpreter.StringResources.TryGetValue( (hInstance, uID), out var value ) )
				{
					// Not found: return 0
					Log.Info( $"LoadStringA: Resource not found (hInstance=0x{hInstance:X8}, uID={uID})" );
					if ( cchBufferMax > 0 )
						Core.WriteByte( lpBuffer, 0 ); // Null-terminate
					return 0;
				}

				// Copy up to cchBufferMax-1 characters, null-terminate
				int len = Math.Min( value.Length, (int)cchBufferMax - 1 );
				for ( int i = 0; i < len; i++ )
					Core.WriteByte( lpBuffer + (uint)i, (byte)value[i] );
				Core.WriteByte( lpBuffer + (uint)len, 0 );

				Log.Info( $"LoadStringA: Loaded \"{value}\" (hInstance=0x{hInstance:X8}, uID={uID}, len={len})" );
				return (uint)len;
			}
		);
		RegisterStdCallFunction<uint, uint, uint, uint, uint>(
			"LoadStringW",
			( hInstance, uID, lpBuffer, cchBufferMax ) =>
			{
				// Try to find the string resource
				if ( !Interpreter.StringResources.TryGetValue( (hInstance, uID), out var value ) )
				{
					Log.Info( $"LoadStringW: Resource not found (hInstance=0x{hInstance:X8}, uID={uID})" );
					if ( cchBufferMax > 0 )
						Core.WriteWord( lpBuffer, 0 ); // Null-terminate (UTF-16)
					return 0;
				}

				// Copy up to cchBufferMax-1 characters, null-terminate
				int len = Math.Min( value.Length, (int)cchBufferMax - 1 );
				for ( int i = 0; i < len; i++ )
					Core.WriteWord( lpBuffer + (uint)(i * 2), value[i] );
				Core.WriteWord( lpBuffer + (uint)(len * 2), 0 );

				Log.Info( $"LoadStringW: Loaded \"{value}\" (hInstance=0x{hInstance:X8}, uID={uID}, len={len})" );
				return (uint)len;
			}
		);

		RegisterStdCallFunction<uint, uint>(
			"GetProcessDefaultLayout",
			( pdwDefaultLayout ) =>
			{
				Log.Info( "GetProcessDefaultLayout called (stub)" );
				if ( pdwDefaultLayout != 0 )
					Core.WriteDword( pdwDefaultLayout, 0 ); // 0 = Left-to-right layout
				return 1; // TRUE
			}
		);
		RegisterStdCallFunction<uint, uint>(
			"CharNextW",
			( lpsz ) =>
			{
				// Read the current WCHAR (2 bytes)
				ushort ch = Core.ReadWord( lpsz );
				Core.LogVerbose( $"CharNextW called (lpsz=0x{lpsz:X8}, char=0x{ch:X4})" );
				if ( ch == 0 )
					return lpsz; // Already at null terminator, don't advance
				return lpsz + 2; // Advance to next WCHAR
			}
		);
		RegisterStdCallFunction<uint, uint, uint>(
			"LoadIconW",
			( hInstance, lpIconName ) =>
			{
				Log.Info( $"LoadIconW called (stub) hInstance=0x{hInstance:X8}, lpIconName=0x{lpIconName:X8}" );
				// Return a fake icon handle (nonzero)
				return 0x10001;
			}
		);

		RegisterStdCallFunction<uint, uint, uint>(
			"LoadCursorW",
			( hInstance, lpCursorName ) =>
			{
				Log.Info( $"LoadCursorW called (stub) hInstance=0x{hInstance:X8}, lpCursorName=0x{lpCursorName:X8}" );
				// Return a fake cursor handle (nonzero)
				return 0x20001;
			}
		);

		RegisterStdCallFunction<uint, uint>(
			"RegisterClassExW",
			( lpwcx ) =>
			{
				Log.Info( $"RegisterClassExW called (stub) lpwcx=0x{lpwcx:X8}" );
				// Return a fake ATOM (nonzero)
				return 0x4001;
			}
		);

		RegisterStdCallFunction<uint, uint>(
			"GetSysColorBrush",
			( nIndex ) =>
			{
				Log.Info( $"GetSysColorBrush called (stub) nIndex={nIndex}" );
				// Return a fake brush handle (nonzero)
				return 0x30001;
			}
		);


		// wsprintfA - Formats a string using variable arguments
		RegisterCdeclVariadicFunction( "wsprintfA", core =>
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
						case '0': // something like 8-digit hex (%08X) or something else, check another char or two ahead
							{
								// Check for a digit or 'X' after '0'
								if ( i + 1 < format.Length && char.IsDigit( format[i + 1] ) )
								{
									// Read the number
									int numDigits = format[i + 1] - '0';
									i++; // Skip the digit
									if ( i + 1 < format.Length && format[i + 1] == 'X' )
									{
										// Hexadecimal formatting
										uint value = core.ReadDword( core.Registers["esp"] + (uint)(paramIndex * 4) );
										result += value.ToString( "X" ).PadLeft( numDigits, '0' );
										paramIndex++;
										i++; // Skip 'X'
									}
									else
									{
										// Just a number, treat it as a string
										result += "%" + specifier;
									}
								}
								else
								{
									result += "%" + specifier; // Unknown format, just append it
								}

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



			// Dump a bit of memory before writing to verify
			Log.Info( "Memory beforing writing (first 32 bytes):" );
			for ( int i = 0; i < 32 && i <= result.Length; i++ )
			{
				byte b = i < result.Length ? (byte)result[i] : (byte)0;
				core.LogVerbose( $"Offset +{i}: 0x{b:X2} '{(b >= 32 && b < 127 ? (char)b : '.')}'" );
			}


			// Write the string and log every 10 bytes for debugging
			for ( int i = 0; i <= result.Length; i++ )
			{
				byte b = i < result.Length ? (byte)result[i] : (byte)0;
				core.WriteByte( bufferPtr + (uint)i, b );

				if ( i % 10 == 0 )
				{
					core.LogVerbose( $"Wrote to 0x{bufferPtr + (uint)i:X8}: {(i < result.Length ? result[i] : '\0')}" );
				}
			}

			// Additional memory dump after writing the string
			Log.Info( "wsprintfA: Memory at bufferPtr after write:" );
			for ( int i = 0; i < 32; i++ )
			{
				byte b = core.ReadByte( bufferPtr + (uint)i );
				core.LogVerbose( $"Offset +{i}: 0x{b:X2} '{(b >= 32 && b < 127 ? (char)b : '.')}'" );
			}

			// Set return value in EAX
			uint returnValue = (uint)result.Length;
			core.Registers["eax"] = returnValue;

			// In cdecl, caller cleans up the stack, so we don't adjust ESP
			// But we do need to set EIP to the return address
			core.Registers["eip"] = returnAddress;

			// HACK HACK! gets the main test executable to work properly? todo: figure out why esp is offset by -4!!!!!!!!!!!
			Log.Warning( $"HACK HACK! Adjusting ESP by 4 to get the test executable to work properly. Todo: Figure out why ESP is offset by -4!!!!!!!!!!!" );
			core.Registers["esp"] += 4;

			Log.Info( $"wsprintfA returning length {returnValue} to 0x{returnAddress:X8}" );

			return returnValue;
		} );
	}
}
