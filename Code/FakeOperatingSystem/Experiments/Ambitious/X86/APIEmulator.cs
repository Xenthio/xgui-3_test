using FakeDesktop;
using FakeOperatingSystem.Experiments.Ambitious.X86.CallConventions;
using System;
using System.Collections.Generic;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public abstract class APIEmulator
{
	protected readonly Dictionary<string, Func<X86Core, uint>> _apiTable = new();
	protected StdCallConvention _stdCallConvention = new();
	protected CdeclConvention _cdeclConvention = new();
	protected X86Core Core { get; private set; }
	protected X86Interpreter Interpreter { get; private set; }

	public bool TryCall( string name, X86Core core, X86Interpreter interpreter, out uint result, bool isJump = false )
	{
		// Set the core for this execution context
		Core = core;
		Interpreter = interpreter;

		try
		{
			// First try the strongly-typed registered functions
			if ( _stdCallConvention.TryCallFunction( name, core, out result, isJump ) )
			{
				return true;
			}

			// Try cdecl functions next
			if ( _cdeclConvention.TryCallFunction( name, core, out result, isJump ) )
			{
				return true;
			}

			// Fall back to the traditional approach
			if ( _apiTable.TryGetValue( name, out var function ) )
			{
				result = function( core );
				return true;
			}

			result = 0;
			return false;
		}
		finally
		{
			// Clear the core reference when done
			Core = null;
		}
	}

	#region Registration helpers

	#region StdCall functions
	protected void RegisterStdCallFunction<TResult>( string name, Func<TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, TResult>( string name, Func<T1, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, TResult>( string name, Func<T1, T2, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, TResult>( string name, Func<T1, T2, T3, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, TResult>( string name, Func<T1, T2, T3, T4, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, TResult>( string name, Func<T1, T2, T3, T4, T5, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, T6, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, T6, T7, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	protected void RegisterStdCallFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> callback )
	{
		_stdCallConvention.RegisterFunction( name, callback );
	}
	#endregion

	#region Cdecl functions
	protected void RegisterCdeclFunction<TResult>( string name, Func<TResult> callback )
	{
		_cdeclConvention.RegisterFunction( name, callback );
	}

	protected void RegisterCdeclFunction<T1, TResult>( string name, Func<T1, TResult> callback )
	{
		_cdeclConvention.RegisterFunction( name, callback );
	}
	protected void RegisterCdeclFunction<T1, T2, TResult>( string name, Func<T1, T2, TResult> callback )
	{
		_cdeclConvention.RegisterFunction( name, callback );
	}
	protected void RegisterCdeclFunction<T1, T2, T3, TResult>( string name, Func<T1, T2, T3, TResult> callback )
	{
		_cdeclConvention.RegisterFunction( name, callback );
	}
	protected void RegisterCdeclFunction<T1, T2, T3, T4, TResult>( string name, Func<T1, T2, T3, T4, TResult> callback )
	{
		_cdeclConvention.RegisterFunction( name, callback );
	}
	protected void RegisterCdeclFunction<T1, T2, T3, T4, T5, TResult>( string name, Func<T1, T2, T3, T4, T5, TResult> callback )
	{
		_cdeclConvention.RegisterFunction( name, callback );
	}
	protected void RegisterCdeclFunction<T1, T2, T3, T4, T5, T6, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, TResult> callback )
	{
		_cdeclConvention.RegisterFunction( name, callback );
	}

	// For variadic functions
	protected void RegisterCdeclVariadicFunction( string name, Func<X86Core, uint> callback )
	{
		_apiTable[name] = core => _cdeclConvention.HandleVariadicCall( core, callback );
	}
	#endregion

	#endregion

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
