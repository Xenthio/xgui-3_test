using FakeDesktop;
using System;
using System.Collections.Generic;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public abstract class APIEmulator
{
	protected readonly Dictionary<string, Func<X86Core, uint>> _apiTable = new();

	public bool TryCall( string name, X86Core core, out uint result )
	{
		Log.Info( $"APIEmulator.TryCall: Looking for '{name}' in [{string.Join( ", ", _apiTable.Keys )}]" );
		if ( _apiTable.TryGetValue( name, out var func ) )
		{
			Log.Info( $"API call: {name}" );
			result = func( core );
			return true;
		}
		result = 0;
		return false;
	}

	public static void ReportMissingExport( X86Interpreter interpreter, string functionName )
	{
		// Try to find which DLL the function should be in
		string dllName = "UNKNOWN.DLL";
		if ( interpreter.ImportSourceDlls.TryGetValue( functionName, out var sourceDll ) )
		{
			dllName = sourceDll;
		}

		interpreter.HaltWithMessageBox(
			$"{interpreter.ExecutableName} - Entry Point Not Found",
			$"The procedure entry point {functionName} could not be located in the dynamic link library {dllName}.",
			MessageBoxIcon.Error
		);
	}
}
