using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86.CallConventions;

public abstract class CallingConvention
{
	public abstract uint HandleCall( X86Core core, Func<object[], uint> function, Type[] parameterTypes, bool isJump = false );

	protected object[] ExtractParameters( X86Core core, Type[] parameterTypes )
	{
		object[] parameters = new object[parameterTypes.Length];

		for ( int i = 0; i < parameterTypes.Length; i++ )
		{
			uint value = core.ReadDword( core.Registers["esp"] + (uint)((i + 1) * 4) );

			parameters[i] = ConvertParameter( core, value, parameterTypes[i] );
		}

		return parameters;
	}

	protected object ConvertParameter( X86Core core, uint value, Type paramType )
	{
		if ( paramType == typeof( uint ) )
			return value;
		else if ( paramType == typeof( int ) )
			return (int)value;
		else if ( paramType == typeof( ushort ) )
			return (ushort)(value & 0xFFFF);
		else if ( paramType == typeof( short ) )
			return (short)(value & 0xFFFF);
		else if ( paramType == typeof( byte ) )
			return (byte)(value & 0xFF);
		else if ( paramType == typeof( sbyte ) )
			return (sbyte)(value & 0xFF);
		else if ( paramType == typeof( string ) )
			return value != 0 ? core.ReadString( value ) : "(null)";
		else if ( paramType == typeof( bool ) )
			return value != 0;

		// Fallback: return raw uint (handles unknown types)
		return value;
	}
}
