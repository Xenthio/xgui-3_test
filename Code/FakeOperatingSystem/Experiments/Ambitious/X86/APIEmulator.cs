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
}
