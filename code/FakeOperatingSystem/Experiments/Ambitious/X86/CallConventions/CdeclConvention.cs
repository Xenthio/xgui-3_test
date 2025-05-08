using System;
using System.Collections.Generic;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.CallConventions;

public class CdeclConvention : CallingConvention
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

		// In cdecl, the caller cleans up the stack, so we DON'T adjust ESP
		// We just set EIP to the return address
		core.Registers["eip"] = returnAddress;

		return result;
	}

	// Register functions with up to 4 parameters
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
	public void RegisterFunction<T1, T2, T3, T4, T5, TResult>( string name, Func<T1, T2, T3, T4, T5, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ) };
	}
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ) };
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

	// Special method for handling variadic functions like printf, sprintf, etc.
	public uint HandleVariadicCall( X86Core core, Func<X86Core, uint> callback )
	{
		// For variadic functions, we pass control directly to the callback
		// so it can manually parse the stack as needed
		uint result = callback( core );

		// The callback is responsible for setting EAX and EIP
		// We just return the result
		return result;
	}
}
