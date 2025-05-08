using System;
using System.Collections.Generic;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.CallConventions;

public class StdCallConvention : CallingConvention
{
	private readonly Dictionary<string, Delegate> _registeredFunctions = new();
	private readonly Dictionary<string, Type[]> _parameterTypes = new();

	public override uint HandleCall( X86Core core, Func<object[], uint> function, Type[] parameterTypes )
	{
		// Get return address
		uint returnAddress = core.ReadDword( core.Registers["esp"] );

		// Extract parameters
		object[] parameters = ExtractParameters( core, parameterTypes );

		// Call the function
		uint result = function( parameters );

		// Set return value in EAX
		core.Registers["eax"] = result;

		// In stdcall, callee cleans up the stack
		core.Registers["esp"] += (uint)(parameterTypes.Length * 4 + 4); // params + return address

		// Jump to return address
		core.Registers["eip"] = returnAddress;

		return result;
	}

	// Register a function with up to 4 parameters
	public void RegisterFunction<TResult>( string name, Func<TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = Type.EmptyTypes;
	}

	public void RegisterFunction<T1, TResult>( string name, Func<T1, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ) };
	}

	public void RegisterFunction<T1, T2, TResult>( string name, Func<T1, T2, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ) };
	}

	public void RegisterFunction<T1, T2, T3, TResult>( string name, Func<T1, T2, T3, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ) };
	}

	public void RegisterFunction<T1, T2, T3, T4, TResult>( string name, Func<T1, T2, T3, T4, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ) };
	}

	public bool TryCallFunction( string name, X86Core core, out uint result )
	{
		result = 0;

		if ( !_registeredFunctions.TryGetValue( name, out var func ) ||
			!_parameterTypes.TryGetValue( name, out var paramTypes ) )
		{
			return false;
		}

		result = HandleCall( core, args =>
		{
			// Invoke the strongly-typed delegate
			var returnValue = func.DynamicInvoke( args );

			// Convert to uint (all Win32 APIs return 32-bit values)
			if ( returnValue == null )
				return 0;

			if ( returnValue is uint uintResult )
				return uintResult;

			if ( returnValue is int intResult )
				return (uint)intResult;

			if ( returnValue is bool boolResult )
				return boolResult ? 1u : 0u;

			return Convert.ToUInt32( returnValue );
		}, paramTypes );

		return true;
	}
}
