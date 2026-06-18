using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.CallConventions;

public class StdCallConvention : CallingConvention
{
	private readonly Dictionary<string, Delegate> _registeredFunctions = new();
	private readonly Dictionary<string, Type[]> _parameterTypes = new();

	// Set by APIEmulator so Task-returning handlers can suspend instead of deadlocking.
	public X86Interpreter Interpreter { get; set; }

	public override uint HandleCall( X86Core core, Func<object[], uint> function, Type[] parameterTypes, bool isJump = false )
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
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, T7, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ), typeof( T7 ) };
	}
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ), typeof( T7 ), typeof( T8 ) };
	}
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ), typeof( T7 ), typeof( T8 ), typeof( T9 ) };
	}
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ), typeof( T7 ), typeof( T8 ), typeof( T9 ), typeof( T10 ) };
	}
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ), typeof( T7 ), typeof( T8 ), typeof( T9 ), typeof( T10 ), typeof( T11 ) };
	}
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ), typeof( T7 ), typeof( T8 ), typeof( T9 ), typeof( T10 ), typeof( T11 ), typeof( T12 ) };
	}
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ), typeof( T7 ), typeof( T8 ), typeof( T9 ), typeof( T10 ), typeof( T11 ), typeof( T12 ), typeof( T13 ) };
	}
	public void RegisterFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult>( string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> callback )
	{
		_registeredFunctions[name] = callback;
		_parameterTypes[name] = new[] { typeof( T1 ), typeof( T2 ), typeof( T3 ), typeof( T4 ), typeof( T5 ), typeof( T6 ), typeof( T7 ), typeof( T8 ), typeof( T9 ), typeof( T10 ), typeof( T11 ), typeof( T12 ), typeof( T13 ), typeof( T14 ) };
	}


	public bool TryCallFunction( string name, X86Core core, out uint result, bool isJump = false )
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
			object returnValue;
			try { returnValue = func.DynamicInvoke( args ); }
			catch ( Exception ex ) when ( ex.GetType().Name == "TargetInvocationException" && ex.InnerException != null )
			{ throw ex.InnerException; }

			// Convert to uint (all Win32 APIs return 32-bit values)
			if ( returnValue == null )
				return 0;

			if ( returnValue is uint uintResult )
				return uintResult;

			if ( returnValue is int intResult )
				return (uint)intResult;

			if ( returnValue is bool boolResult )
				return boolResult ? 1u : 0u;

			// Handle async calls — never block with .WaitAll / .Result on main thread (deadlock).
			// Instead, suspend the interpreter loop until the task completes.
			if ( returnValue is Task taskValue )
			{
				if ( !taskValue.IsCompleted )
				{
					if ( Interpreter != null )
					{
						// Suspend the emulated CPU; resume after task completes, then read result.
						var tcs = new TaskCompletionSource<uint>();
						Interpreter.SuspendForTask( tcs.Task );
						_ = GameTask.RunInThreadAsync( async () =>
						{
							await GameTask.MainThread();
							await taskValue;
							uint taskResult = 0;
							if ( taskValue is Task<uint> ut ) taskResult = ut.Result;
							else if ( taskValue is Task<int> it ) taskResult = (uint)it.Result;
							else if ( taskValue is Task<bool> bt ) taskResult = bt.Result ? 1u : 0u;
							// Write result into EAX before resuming
							Interpreter.Core.Registers["eax"] = taskResult;
							tcs.TrySetResult( taskResult );
						} );
						// Return 0 as placeholder; real result written to EAX when task resumes
						return 0;
					}
					// No interpreter reference — fall back to blocking (standalone tests only)
					taskValue.Wait();
				}
				if ( taskValue is Task<uint> uintTask ) return uintTask.Result;
				if ( taskValue is Task<int> intTask ) return (uint)intTask.Result;
				if ( taskValue is Task<bool> boolTask ) return boolTask.Result ? 1u : 0u;
				return 0;
			}


			// Add this check for unsupported types
			if ( !(returnValue is IConvertible) )
				throw new InvalidOperationException( $"Return value of type '{returnValue.GetType()}' cannot be converted to uint." );


			return Convert.ToUInt32( returnValue );
		}, paramTypes, isJump );

		return true;
	}
}
