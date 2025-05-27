using FakeOperatingSystem.OSFileSystem;
using System;
using System.Collections.Generic;
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
			Root = JsonSerializer.Deserialize<RegistryKey>( json ) ?? new RegistryKey();
		}
		else
		{
			// Create an empty hive file if it doesn't exist
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
		AddHive( "HKEY_USERS\\.DEFAULT", @"C:\Windows\System32\config\DEFAULT" );
		// No user system yet. Default user.
		AddHive( "HKEY_CURRENT_USER", @"C:\Windows\USER.DAT" );
		AddHive( "HKEY_CURRENT_CONFIG", @"C:\Windows\System32\config\CONFIG" );
		AddHive( "HKEY_DYN_DATA", @"C:\Windows\System32\config\DYN_DATA" );

		Instance = this;
	}

	public void AddHive( string rootKey, string filePath )
	{
		_hives[rootKey] = new RegistryHive( rootKey, filePath );
	}

	// Helper to find the hive and subkey path
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

	public object GetValue( string keyPath, string valueName, object defaultValue = null )
	{
		var (hive, subPath) = Resolve( keyPath );
		var key = GetKey( hive.Root, subPath, false );
		if ( key != null && key.Values.TryGetValue( valueName, out var value ) )
			return value;
		return defaultValue;
	}

	public void SetValue( string keyPath, string valueName, object value )
	{
		var (hive, subPath) = Resolve( keyPath );
		var key = GetKey( hive.Root, subPath, true );
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
		if ( parts.Length == 0 ) return;
		var parent = GetKey( hive.Root, string.Join( '\\', parts[..^1] ), false );
		if ( parent != null )
		{
			parent.SubKeys.Remove( parts[^1] );
			hive.Save();
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
