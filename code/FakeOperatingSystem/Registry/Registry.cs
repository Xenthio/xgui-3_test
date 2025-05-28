using FakeOperatingSystem.OSFileSystem;
using System;
using System.Collections.Generic;
using System.Globalization; // Required for parsing numbers robustly
using System.Text.Json;

namespace FakeOperatingSystem;

public class RegistryKey
{
	// Initialize with a case-insensitive comparer for SubKeys
	public Dictionary<string, RegistryKey> SubKeys { get; set; } = new( StringComparer.OrdinalIgnoreCase );

	// Initialize with a case-insensitive comparer for Values
	public Dictionary<string, object> Values { get; set; } = new( StringComparer.OrdinalIgnoreCase );
}

public class RegistryHive
{
	public string Name { get; }
	public string FilePath { get; } // Can be a placeholder for "virtual" hives like HKLM/HKU containers
	public RegistryKey Root { get; internal set; } = new(); // Made internal set for constructor flexibility

	// Constructor for hives backed by a single file
	public RegistryHive( string name, string filePath )
	{
		Name = name;
		FilePath = filePath;
		Load();
	}

	// Constructor for "virtual" hives or those populated manually (like HKLM/HKU containers)
	public RegistryHive( string name, RegistryKey rootKey, string placeholderPath = null )
	{
		Name = name;
		Root = rootKey;
		FilePath = placeholderPath; // May not be used for saving if it's just a container
	}

	public void Load()
	{
		if ( string.IsNullOrEmpty( FilePath ) ) // Don't load if no file path (e.g. virtual HKLM root)
		{
			Root = new RegistryKey(); // Ensure it has a root
			return;
		}

		if ( VirtualFileSystem.Instance.FileExists( FilePath ) )
		{
			var json = VirtualFileSystem.Instance.ReadAllText( FilePath );
			Root = JsonSerializer.Deserialize<RegistryKey>( json ) ?? new RegistryKey();
		}
		else
		{
			Root = new RegistryKey();
			Save(); // Save if the file was expected but missing
		}
	}

	public void Save()
	{
		if ( string.IsNullOrEmpty( FilePath ) ) // Don't save if no file path
			return;

		var json = JsonSerializer.Serialize( Root, new JsonSerializerOptions { WriteIndented = true } );
		VirtualFileSystem.Instance.WriteAllText( FilePath, json );
	}
}

public class Registry
{
	private Dictionary<string, RegistryHive> _hives = new();
	public static Registry Instance { get; private set; }
	public IEnumerable<string> RootHiveNames => _hives.Keys;
	public IEnumerable<RegistryHive> RootHives => _hives.Values;

	public Registry()
	{
		Instance = this;

		// --- HKEY_LOCAL_MACHINE ---
		var hklmRootKey = new RegistryKey();
		// The FilePath for HKLM itself is a placeholder; its content comes from other files.
		var hklmHive = new RegistryHive( "HKEY_LOCAL_MACHINE", hklmRootKey, @"C:\Windows\System32\config\HKLM_VIRTUAL.hive" );

		RegistryKey systemKey = LoadKeyFromFile( @"C:\Windows\System32\config\SYSTEM" );
		if ( systemKey != null ) hklmRootKey.SubKeys["SYSTEM"] = systemKey;

		RegistryKey softwareKey = LoadKeyFromFile( @"C:\Windows\System32\config\SOFTWARE" );
		if ( softwareKey != null ) hklmRootKey.SubKeys["SOFTWARE"] = softwareKey;

		RegistryKey networkKey = LoadKeyFromFile( @"C:\Windows\System32\config\NETWORK" );
		if ( networkKey != null ) hklmRootKey.SubKeys["NETWORK"] = networkKey;
		// Add SAM, SECURITY, HARDWARE similarly if you have files for them.
		_hives["HKEY_LOCAL_MACHINE"] = hklmHive;

		// --- HKEY_USERS ---
		var hkuRootKey = new RegistryKey();
		// FilePath for HKU is also a placeholder.
		var hkuHive = new RegistryHive( "HKEY_USERS", hkuRootKey, @"C:\Windows\System32\config\HKU_VIRTUAL.hive" );

		RegistryKey defaultUserKey = LoadKeyFromFile( @"C:\Windows\System32\config\DEFAULT" );
		if ( defaultUserKey != null ) hkuRootKey.SubKeys[".DEFAULT"] = defaultUserKey;
		_hives["HKEY_USERS"] = hkuHive;

		// --- HKEY_CURRENT_USER ---
		// This is a distinct hive, loaded per user. Initial one points to a default.
		_hives["HKEY_CURRENT_USER"] = new RegistryHive( "HKEY_CURRENT_USER", @"C:\Windows\USER.DAT" );

		// --- HKEY_CURRENT_CONFIG ---
		_hives["HKEY_CURRENT_CONFIG"] = new RegistryHive( "HKEY_CURRENT_CONFIG", @"C:\Windows\System32\config\CONFIG" );

		// HKEY_DYN_DATA (Optional, Win9x specific)
		// _hives["HKEY_DYN_DATA"] = new RegistryHive("HKEY_DYN_DATA", @"C:\Windows\System32\config\DYN_DATA");
	}

	// Helper to load a RegistryKey structure from a specific file.
	// These files (SYSTEM, SOFTWARE, etc.) now represent the content of a single key.
	private RegistryKey LoadKeyFromFile( string filePath )
	{
		if ( VirtualFileSystem.Instance.FileExists( filePath ) )
		{
			var json = VirtualFileSystem.Instance.ReadAllText( filePath );
			return JsonSerializer.Deserialize<RegistryKey>( json ) ?? new RegistryKey();
		}
		else
		{
			// If a component file (like SYSTEM) is missing, create an empty key and save it.
			var newKey = new RegistryKey();
			var json = JsonSerializer.Serialize( newKey, new JsonSerializerOptions { WriteIndented = true } );
			VirtualFileSystem.Instance.WriteAllText( filePath, json );
			Log.Info( $"Registry: Created default empty key file at {filePath}" );
			return newKey;
		}
	}

	// Public AddHive is for adding completely new top-level hives if ever needed by external tools.
	// Not typically used for standard Windows hives after initial setup.
	public void AddHive( string rootHiveName, string filePath )
	{
		if ( _hives.ContainsKey( rootHiveName ) )
		{
			Log.Warning( $"Registry: Hive '{rootHiveName}' already exists. Overwriting." );
		}
		_hives[rootHiveName] = new RegistryHive( rootHiveName, filePath );
	}

	public void LoadUserHive( string userName, string userHiveFilePath )
	{
		// 1. Update HKEY_CURRENT_USER to point to this user's hive file
		_hives["HKEY_CURRENT_USER"] = new RegistryHive( "HKEY_CURRENT_USER", userHiveFilePath );

		// 2. Mount this user's hive data under HKEY_USERS\<UserNameOrSID>
		//    In a real system, userName would map to a SID. For simplicity, we use userName.
		if ( _hives.TryGetValue( "HKEY_USERS", out RegistryHive hkuHive ) )
		{
			RegistryKey userSpecificDataRoot = LoadKeyFromFile( userHiveFilePath ); // Load the content of NTUSER.DAT
			if ( userSpecificDataRoot != null )
			{
				hkuHive.Root.SubKeys[userName] = userSpecificDataRoot;
				// Note: This modification to hkuHive.Root is in-memory. 
				// The HKU_VIRTUAL.hive file (if it existed) wouldn't be saved by this,
				// which is fine as HKU is a container of other hives.
			}
		}
	}

	private (RegistryHive hive, string subPath) Resolve( string keyPath )
	{
		RegistryHive matchedHive = null;
		string longestMatch = "";

		foreach ( var hiveNameInDict in _hives.Keys )
		{
			// We need to match "HKEY_LOCAL_MACHINE" against "HKEY_LOCAL_MACHINE\SYSTEM"
			if ( keyPath.StartsWith( hiveNameInDict, StringComparison.OrdinalIgnoreCase ) )
			{
				// Ensure it's a full word match for the hive name or followed by a separator
				if ( keyPath.Length == hiveNameInDict.Length || keyPath[hiveNameInDict.Length] == '\\' )
				{
					if ( hiveNameInDict.Length > longestMatch.Length )
					{
						longestMatch = hiveNameInDict;
						matchedHive = _hives[hiveNameInDict];
					}
				}
			}
		}

		if ( matchedHive != null )
		{
			var subPath = keyPath.Length > longestMatch.Length
				? keyPath.Substring( longestMatch.Length ).TrimStart( '\\' )
				: "";
			return (matchedHive, subPath);
		}
		throw new Exception( "Hive not found for key: " + keyPath );
	}

	public T GetValue<T>( string keyPath, string valueName, T defaultValue = default )
	{
		var (hive, subPath) = Resolve( keyPath );
		var key = GetKey( hive.Root, subPath, false ); // GetKey will navigate using subPath from hive.Root
		if ( key != null && key.Values.TryGetValue( valueName, out var valueObj ) )
		{
			if ( Nullable.GetUnderlyingType( typeof( T ) ) != null && valueObj == null )
				return defaultValue;
			if ( valueObj is T typedValue )
				return typedValue;
			if ( valueObj is JsonElement jsonElement )
			{
				if ( Nullable.GetUnderlyingType( typeof( T ) ) != null && jsonElement.ValueKind == JsonValueKind.Null )
					return defaultValue;
				try
				{
					if ( typeof( T ) == typeof( int ) && jsonElement.TryGetInt32( out int intVal ) ) return (T)(object)intVal;
					if ( typeof( T ) == typeof( int? ) && jsonElement.TryGetInt32( out int intNullableVal ) ) return (T)(object)intNullableVal;
					if ( typeof( T ) == typeof( long ) && jsonElement.TryGetInt64( out long longVal ) ) return (T)(object)longVal;
					if ( typeof( T ) == typeof( long? ) && jsonElement.TryGetInt64( out long longNullableVal ) ) return (T)(object)longNullableVal;
					if ( typeof( T ) == typeof( string ) ) return (T)(object)jsonElement.GetString();
					if ( typeof( T ) == typeof( bool ) && (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False) ) return (T)(object)jsonElement.GetBoolean();
					if ( typeof( T ) == typeof( bool? ) && (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False) ) return (T)(object)jsonElement.GetBoolean();
					if ( typeof( T ) == typeof( float ) && jsonElement.TryGetSingle( out float floatVal ) ) return (T)(object)floatVal;
					if ( typeof( T ) == typeof( float? ) && jsonElement.TryGetSingle( out float floatNullableVal ) ) return (T)(object)floatNullableVal;
					if ( typeof( T ) == typeof( double ) && jsonElement.TryGetDouble( out double doubleVal ) ) return (T)(object)doubleVal;
					if ( typeof( T ) == typeof( double? ) && jsonElement.TryGetDouble( out double doubleNullableVal ) ) return (T)(object)doubleNullableVal;
					if ( typeof( T ) == typeof( decimal ) && jsonElement.TryGetDecimal( out decimal decimalVal ) ) return (T)(object)decimalVal;
					if ( typeof( T ) == typeof( decimal? ) && jsonElement.TryGetDecimal( out decimal decimalNullableVal ) ) return (T)(object)decimalNullableVal;
					if ( typeof( T ) == typeof( DateTime ) && jsonElement.TryGetDateTime( out DateTime dateTimeVal ) ) return (T)(object)dateTimeVal;
					if ( typeof( T ) == typeof( DateTime? ) && jsonElement.TryGetDateTime( out DateTime dateTimeNullableVal ) ) return (T)(object)dateTimeNullableVal;
					if ( typeof( T ) == typeof( Guid ) && jsonElement.TryGetGuid( out Guid guidVal ) ) return (T)(object)guidVal;
					if ( typeof( T ) == typeof( Guid? ) && jsonElement.TryGetGuid( out Guid guidNullableVal ) ) return (T)(object)guidNullableVal;
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
			catch { /* Conversion failed */ }
		}
		return defaultValue;
	}

	public void SetValue( string keyPath, string valueName, object value )
	{
		var (hive, subPath) = Resolve( keyPath );
		var key = GetKey( hive.Root, subPath, true );
		key.Values[valueName] = value;
		// Saving the individual component hive (SYSTEM.json, etc.) needs to be handled
		// if the change was within such a component.
		// The current hive.Save() would save HKLM_VIRTUAL.hive or HKU_VIRTUAL.hive, which is not what we want for subkey changes.
		// This requires a more sophisticated save mechanism or saving the component file directly.
		// For now, let's assume component files are saved by LoadKeyFromFile if created,
		// and modifications to existing component files need a dedicated save path.
		// A simple solution: if hive.FilePath points to a "VIRTUAL" hive, don't save it.
		// The actual component file (e.g. SYSTEM) would need to be identified and saved.
		// This is complex. For now, let's rely on the fact that the RegistryKey objects are modified in memory.
		// A full save strategy would be:
		// 1. Determine which original file this keyPath belongs to (e.g. SYSTEM, SOFTWARE, or USER.DAT)
		// 2. Save that specific file.
		// For now, only hives with non-virtual FilePaths (like HKCU) will save correctly on SetValue.
		if ( hive.FilePath != null && !hive.FilePath.Contains( "_VIRTUAL." ) )
		{
			hive.Save();
		}
		else
		{
			// Need to find which original file to save for HKLM subkeys
			// e.g. if keyPath is HKLM\SYSTEM\MyKey, save SYSTEM file.
			// This logic is not yet implemented here.
			Log.Warning( $"Registry: SetValue on virtual hive '{hive.Name}'. Component file not saved automatically. Path: {keyPath}" );
		}
	}

	public void DeleteValue( string keyPath, string valueName )
	{
		var (hive, subPath) = Resolve( keyPath );
		var key = GetKey( hive.Root, subPath, false );
		if ( key != null && key.Values.Remove( valueName ) )
		{
			// Similar saving issue as SetValue for virtual hives
			if ( hive.FilePath != null && !hive.FilePath.Contains( "_VIRTUAL." ) )
			{
				hive.Save();
			}
			else
			{
				Log.Warning( $"Registry: DeleteValue on virtual hive '{hive.Name}'. Component file not saved automatically. Path: {keyPath}" );
			}
		}
	}

	public void DeleteKey( string keyPath )
	{
		var (hive, subPath) = Resolve( keyPath );
		var parts = subPath.Split( '\\', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 && (hive.Name == "HKEY_LOCAL_MACHINE" || hive.Name == "HKEY_USERS") )
		{
			// Trying to delete a main component like "SYSTEM" from HKLM
			if ( hive.Root.SubKeys.Remove( subPath ) ) // subPath would be "SYSTEM"
			{
				Log.Info( $"Registry: Deleted component key '{subPath}' from virtual hive '{hive.Name}'. The backing file is not deleted by this operation." );
				// No direct hive.Save() for the virtual hive. The component file (e.g. SYSTEM.json) would still exist.
				// To truly delete it, you'd delete the file and then remove from SubKeys.
			}
			return;
		}
		if ( parts.Length == 0 && hive.FilePath != null && !hive.FilePath.Contains( "_VIRTUAL." ) )
		{
			// Trying to delete the root of a file-backed hive (e.g. HKCU). Clear it instead?
			Log.Warning( $"Registry: Attempted to delete root of hive '{keyPath}'. Clearing root instead." );
			hive.Root.SubKeys.Clear();
			hive.Root.Values.Clear();
			hive.Save();
			return;
		}


		var parentKey = GetKey( hive.Root, string.Join( '\\', parts[..^1] ), false );
		if ( parentKey != null )
		{
			if ( parentKey.SubKeys.Remove( parts[^1] ) )
			{
				if ( hive.FilePath != null && !hive.FilePath.Contains( "_VIRTUAL." ) )
				{
					hive.Save();
				}
				else
				{
					Log.Warning( $"Registry: DeleteKey on virtual hive '{hive.Name}'. Component file not saved automatically. Path: {keyPath}" );
				}
			}
		}
	}

	// GetKey remains the same, it navigates from the provided root using the subPath.
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
