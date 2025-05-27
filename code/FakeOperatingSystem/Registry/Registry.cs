using FakeOperatingSystem.OSFileSystem;
using System;
using System.Collections.Generic;
using System.Globalization; // Required for parsing numbers robustly
using System.Text.Json;

namespace FakeOperatingSystem;

public class RegistryKey
{
	public Dictionary<string, RegistryKey> SubKeys { get; set; } = new();
	public Dictionary<string, object> Values { get; set; } = new();
}

public class RegistryHive
{
	public string Name { get; }
	public string FilePath { get; }
	public RegistryKey Root { get; private set; } = new();

	public RegistryHive( string name, string filePath )
	{
		Name = name;
		FilePath = filePath;
		Load();
	}

	public void Load()
	{
		if ( VirtualFileSystem.Instance.FileExists( FilePath ) )
		{
			var json = VirtualFileSystem.Instance.ReadAllText( FilePath );
			// JsonSerializer.Deserialize will create JsonElement for object properties
			Root = JsonSerializer.Deserialize<RegistryKey>( json ) ?? new RegistryKey();
		}
		else
		{
			Root = new RegistryKey();
			Save();
		}
	}

	public void Save()
	{
		var json = JsonSerializer.Serialize( Root, new JsonSerializerOptions { WriteIndented = true } );
		VirtualFileSystem.Instance.WriteAllText( FilePath, json );
	}
}

public class Registry
{
	private Dictionary<string, RegistryHive> _hives = new();
	public static Registry Instance { get; private set; }

	public Registry()
	{
		AddHive( "HKEY_LOCAL_MACHINE\\SYSTEM", @"C:\Windows\System32\config\SYSTEM" );
		AddHive( "HKEY_LOCAL_MACHINE\\SOFTWARE", @"C:\Windows\System32\config\SOFTWARE" );
		AddHive( "HKEY_LOCAL_MACHINE\\NETWORK", @"C:\Windows\System32\config\NETWORK" );
		AddHive( "HKEY_USERS\\.DEFAULT", @"C:\Windows\System32\config\DEFAULT" );
		AddHive( "HKEY_CURRENT_USER", @"C:\Windows\USER.DAT" ); // Default path, might be overridden by UserManager
		AddHive( "HKEY_CURRENT_CONFIG", @"C:\Windows\System32\config\CONFIG" );
		AddHive( "HKEY_DYN_DATA", @"C:\Windows\System32\config\DYN_DATA" );

		Instance = this;
	}

	public void AddHive( string rootKey, string filePath )
	{
		_hives[rootKey] = new RegistryHive( rootKey, filePath );
	}

	public void LoadUserHive( string userName, string hivePath )
	{
		// Unload existing HKEY_CURRENT_USER if any, or handle appropriately
		// For simplicity, this example replaces or adds it.
		// In a real scenario, you'd ensure only one HKCU is active.
		string hkcuKey = "HKEY_CURRENT_USER";
		_hives[hkcuKey] = new RegistryHive( hkcuKey, hivePath );
		// Optionally, you might want to associate this hive with the userName
		// if you need to manage multiple loaded user hives (not typical for HKCU).
	}


	private (RegistryHive hive, string subPath) Resolve( string keyPath )
	{
		foreach ( var kvp in _hives )
		{
			if ( keyPath.StartsWith( kvp.Key, StringComparison.OrdinalIgnoreCase ) )
			{
				var subPath = keyPath.Length > kvp.Key.Length
					? keyPath.Substring( kvp.Key.Length ).TrimStart( '\\' )
					: "";
				return (kvp.Value, subPath);
			}
		}
		throw new Exception( "Hive not found for key: " + keyPath );
	}

	public T GetValue<T>( string keyPath, string valueName, T defaultValue = default )
	{
		var (hive, subPath) = Resolve( keyPath );
		var key = GetKey( hive.Root, subPath, false );
		if ( key != null && key.Values.TryGetValue( valueName, out var valueObj ) )
		{
			// If T is nullable and the underlying value is null, return null (or default for T which is null for nullable types)
			if ( Nullable.GetUnderlyingType( typeof( T ) ) != null && valueObj == null )
			{
				return defaultValue; // Which will be null for T?
			}

			if ( valueObj is T typedValue )
			{
				return typedValue;
			}

			if ( valueObj is JsonElement jsonElement )
			{
				// Handle JsonElement.ValueKind == JsonValueKind.Null for nullable types
				if ( Nullable.GetUnderlyingType( typeof( T ) ) != null && jsonElement.ValueKind == JsonValueKind.Null )
				{
					return defaultValue; // default(T?) is null
				}

				try
				{
					// Attempt to convert JsonElement to the target type T
					if ( typeof( T ) == typeof( int ) && jsonElement.TryGetInt32( out int intVal ) ) return (T)(object)intVal;
					if ( typeof( T ) == typeof( int? ) && jsonElement.TryGetInt32( out int intNullableVal ) ) return (T)(object)intNullableVal; // Handle int?
					if ( typeof( T ) == typeof( long ) && jsonElement.TryGetInt64( out long longVal ) ) return (T)(object)longVal;
					if ( typeof( T ) == typeof( long? ) && jsonElement.TryGetInt64( out long longNullableVal ) ) return (T)(object)longNullableVal; // Handle long?
					if ( typeof( T ) == typeof( string ) ) return (T)(object)jsonElement.GetString(); // GetString can return null
					if ( typeof( T ) == typeof( bool ) && (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False) ) return (T)(object)jsonElement.GetBoolean();
					if ( typeof( T ) == typeof( bool? ) && (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False) ) return (T)(object)jsonElement.GetBoolean(); // Handle bool?
					if ( typeof( T ) == typeof( float ) && jsonElement.TryGetSingle( out float floatVal ) ) return (T)(object)floatVal;
					if ( typeof( T ) == typeof( float? ) && jsonElement.TryGetSingle( out float floatNullableVal ) ) return (T)(object)floatNullableVal; // Handle float?
					if ( typeof( T ) == typeof( double ) && jsonElement.TryGetDouble( out double doubleVal ) ) return (T)(object)doubleVal;
					if ( typeof( T ) == typeof( double? ) && jsonElement.TryGetDouble( out double doubleNullableVal ) ) return (T)(object)doubleNullableVal; // Handle double?
					if ( typeof( T ) == typeof( decimal ) && jsonElement.TryGetDecimal( out decimal decimalVal ) ) return (T)(object)decimalVal;
					if ( typeof( T ) == typeof( decimal? ) && jsonElement.TryGetDecimal( out decimal decimalNullableVal ) ) return (T)(object)decimalNullableVal; // Handle decimal?
					if ( typeof( T ) == typeof( DateTime ) && jsonElement.TryGetDateTime( out DateTime dateTimeVal ) ) return (T)(object)dateTimeVal;
					if ( typeof( T ) == typeof( DateTime? ) && jsonElement.TryGetDateTime( out DateTime dateTimeNullableVal ) ) return (T)(object)dateTimeNullableVal; // Handle DateTime?
					if ( typeof( T ) == typeof( Guid ) && jsonElement.TryGetGuid( out Guid guidVal ) ) return (T)(object)guidVal;
					if ( typeof( T ) == typeof( Guid? ) && jsonElement.TryGetGuid( out Guid guidNullableVal ) ) return (T)(object)guidNullableVal; // Handle Guid?

					return JsonSerializer.Deserialize<T>( jsonElement.GetRawText() );
				}
				catch ( Exception ex )
				{
					Log.Warning( $"[JsonRegistry] Error converting JsonElement to {typeof( T )} for {keyPath}\\{valueName}: {ex.Message}" );
					return defaultValue;
				}
			}

			try
			{
				return (T)Convert.ChangeType( valueObj, Nullable.GetUnderlyingType( typeof( T ) ) ?? typeof( T ), CultureInfo.InvariantCulture );
			}
			catch ( InvalidCastException ) { /* Conversion failed */ }
			catch ( FormatException ) { /* Conversion failed */ }
			catch ( OverflowException ) { /* Conversion failed */ }
		}
		return defaultValue;
	}


	public void SetValue( string keyPath, string valueName, object value )
	{
		var (hive, subPath) = Resolve( keyPath );
		var key = GetKey( hive.Root, subPath, true );
		// When value is set, it's stored as its actual C# type.
		// JsonSerializer will handle serializing these types correctly.
		key.Values[valueName] = value;
		hive.Save();
	}

	public void DeleteValue( string keyPath, string valueName )
	{
		var (hive, subPath) = Resolve( keyPath );
		var key = GetKey( hive.Root, subPath, false );
		if ( key != null && key.Values.Remove( valueName ) )
			hive.Save();
	}

	public void DeleteKey( string keyPath )
	{
		var (hive, subPath) = Resolve( keyPath );
		var parts = subPath.Split( '\\', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 ) return; // Cannot delete a root hive this way
		var parent = GetKey( hive.Root, string.Join( '\\', parts[..^1] ), false );
		if ( parent != null )
		{
			if ( parent.SubKeys.Remove( parts[^1] ) )
			{
				hive.Save();
			}
		}
	}

	private RegistryKey GetKey( RegistryKey root, string keyPath, bool create )
	{
		if ( string.IsNullOrEmpty( keyPath ) ) return root;
		var parts = keyPath.Split( '\\', StringSplitOptions.RemoveEmptyEntries );
		var current = root;
		foreach ( var part in parts )
		{
			if ( !current.SubKeys.TryGetValue( part, out var next ) )
			{
				if ( create )
				{
					next = new RegistryKey();
					current.SubKeys[part] = next;
				}
				else
				{
					return null;
				}
			}
			current = next;
		}
		return current;
	}
}
