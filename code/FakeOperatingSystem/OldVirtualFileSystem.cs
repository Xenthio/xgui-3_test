using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FakeDesktop;

/// <summary>
/// A virtual file system, kinda like shell namespaces (i think)
/// </summary>
[Obsolete]
public class OldVirtualFileSystem
{
	// Special folder constants
	public const string DESKTOP = "Desktop";
	public const string MY_COMPUTER = "My Computer";
	public const string MY_DOCUMENTS = "My Documents";
	public const string RECYCLE_BIN = "Recycle Bin";
	public const string NETWORK_NEIGHBORHOOD = "Network Neighborhood";
	public const string CONTROL_PANEL = "Control Panel";
	public const string PRINTERS = "Printers";
	public const string DIAL_UP_NETWORKING = "Dial-Up Networking";
	public const string SCHEDULED_TASKS = "Scheduled Tasks";

	// Central mapping of virtual paths to their real targets and metadata
	private Dictionary<string, VirtualEntry> _virtualPaths = new();

	// The real filesystem used to store actual files
	private BaseFileSystem _realFileSystem;

	// Root of the real data in the filesystem
	private string _rootPath;

	// Cached files and folders for faster browsing
	private Dictionary<string, List<VirtualEntry>> _cachedDirectoryContents = new();

	// File associations for handling file types
	private Dictionary<string, FileAssociation> _fileAssociations = new Dictionary<string, FileAssociation>( StringComparer.OrdinalIgnoreCase );

	public static OldVirtualFileSystem Instance { get; private set; } = null!;

	public OldVirtualFileSystem( BaseFileSystem realFileSystem, string rootPath )
	{
		Instance = this;
		_realFileSystem = realFileSystem;
		_rootPath = rootPath;

		// Create the root structure if it doesn't exist
		SetupFileSystem();

		// Create the virtual path mappings
		SetupVirtualMappings();
	}

	/// <summary>
	/// Set up the real file storage structure
	/// </summary>
	private void SetupFileSystem()
	{
		// Set up default file associations
		SetupDefaultFileAssociations();
	}

	private void SetupDefaultFileAssociations()
	{
		// Text files
		var txtAssociation = new FileAssociation( ".txt", "Text Document", "txt", "notepad.exe", shouldShowInShellCreateNew: true );
		RegisterFileAssociation( txtAssociation );

		// HTML files
		var htmlAssociation = new FileAssociation( ".html", "HTML Document", "html", "iexplore.exe" );
		htmlAssociation.AddAction( "edit", "Edit", "notepad.exe" );
		RegisterFileAssociation( htmlAssociation );

		// WAD files (for Doom)
		var wadAssociation = new FileAssociation( ".wad", "Doom WAD File", "wad", "doom95.exe" );
		RegisterFileAssociation( wadAssociation );

		// Shortcuts
		var lnkAssociation = new FileAssociation( ".lnk", "Shortcut", "lnk", null );
		// Shortcuts are handled specially in the file browser
		RegisterFileAssociation( lnkAssociation );

		// Ini files
		var iniAssociation = new FileAssociation( ".ini", "INI File", "ini", "notepad.exe" );
		iniAssociation.AddAction( "edit", "Edit", "notepad.exe" );
		RegisterFileAssociation( iniAssociation );

		// Add more associations as needed
	}


	/// <summary>
	/// Set up the virtual path mappings to real storage paths or other behaviors
	/// </summary>
	private void SetupVirtualMappings()
	{
		// Clear existing mappings
		_virtualPaths.Clear();
		_cachedDirectoryContents.Clear();

		// Desktop is the root of the virtual file system
		_virtualPaths[DESKTOP] = new VirtualEntry
		{
			Name = DESKTOP,
			Path = DESKTOP,
			RealPath = "FakeSystemRoot/Windows/Desktop",
			Type = EntryType.SpecialFolder,
			IsVirtual = false,
			IconName = "desktop"
		};

		// My Computer - special folder to hold drives and system folders
		_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}"] = new VirtualEntry
		{
			Name = MY_COMPUTER,
			Path = $"{DESKTOP}/{MY_COMPUTER}",
			RealPath = null, // No direct real path - it's fully virtual
			Type = EntryType.SpecialFolder,
			IsVirtual = true,
			IconName = "mycomputer"
		};

		// My Documents
		_virtualPaths[$"{DESKTOP}/{MY_DOCUMENTS}"] = new VirtualEntry
		{
			Name = MY_DOCUMENTS,
			Path = $"{DESKTOP}/{MY_DOCUMENTS}",
			RealPath = "FakeSystemRoot/My Documents",
			Type = EntryType.SpecialFolder,
			IsVirtual = false, // Points to a real directory
			IconName = "mydocuments"
		};

		// Network Neighborhood
		_virtualPaths[$"{DESKTOP}/{NETWORK_NEIGHBORHOOD}"] = new VirtualEntry
		{
			Name = NETWORK_NEIGHBORHOOD,
			Path = $"{DESKTOP}/{NETWORK_NEIGHBORHOOD}",
			RealPath = null,
			Type = EntryType.SpecialFolder,
			IsVirtual = true,
			IconName = "networkneighborhood"
		};

		// Recycle Bin
		_virtualPaths[$"{DESKTOP}/{RECYCLE_BIN}"] = new VirtualEntry
		{
			Name = RECYCLE_BIN,
			Path = $"{DESKTOP}/{RECYCLE_BIN}",
			RealPath = "FakeSystemRoot/Recycled", // Will be created if needed
			Type = EntryType.SpecialFolder,
			IsVirtual = false,
			IconName = "recyclebinempty"
		};

		{
			// C: Drive (inside My Computer)
			_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}/C:"] = new VirtualEntry
			{
				Name = "(C:)",
				Path = $"{DESKTOP}/{MY_COMPUTER}/C:",
				RealPath = "FakeSystemRoot",
				Type = EntryType.Drive,
				IsVirtual = false,
				IconName = "drive"
			};

			// D: Drive (inside My Computer) - this points to the Mounted filesystem
			_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}/FS:A:"] = new VirtualEntry
			{
				Name = "Mounted (FS:A:)",
				Path = $"{DESKTOP}/{MY_COMPUTER}/FS:A:",
				RealPath = "/",
				Type = EntryType.Drive,
				IsVirtual = false,
				IconName = "drive",
				AssociatedFileSystem = FileSystem.Mounted
			};

			// Y: Drive (inside My Computer) - this points to the OrganizationData filesystem
			_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}/FS:B:"] = new VirtualEntry
			{
				Name = "OrgData (FS:B:)",
				Path = $"{DESKTOP}/{MY_COMPUTER}/FS:B:",
				RealPath = "/",
				Type = EntryType.Drive,
				IsVirtual = false,
				IconName = "drive",
				AssociatedFileSystem = FileSystem.OrganizationData
			};

			// Z: Drive (inside My Computer) - this points to the Data filesystem
			_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}/FS:C:"] = new VirtualEntry
			{
				Name = "Data (FS:C:)",
				Path = $"{DESKTOP}/{MY_COMPUTER}/FS:C:",
				RealPath = "/",
				Type = EntryType.Drive,
				IsVirtual = false,
				IconName = "drive",
				AssociatedFileSystem = FileSystem.Data
			};

			// Control Panel (inside My Computer)
			_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}/{CONTROL_PANEL}"] = new VirtualEntry
			{
				Name = CONTROL_PANEL,
				Path = $"{DESKTOP}/{MY_COMPUTER}/{CONTROL_PANEL}",
				RealPath = null,
				Type = EntryType.ControlPanel,
				IsVirtual = true,
				IconName = "controlpanel"
			};

			// Printers folder (inside My Computer)
			_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}/{PRINTERS}"] = new VirtualEntry
			{
				Name = PRINTERS,
				Path = $"{DESKTOP}/{MY_COMPUTER}/{PRINTERS}",
				RealPath = null,
				Type = EntryType.SpecialFolder,
				IsVirtual = true,
				IconName = "printer"
			};

			// Dial-Up Networking (inside My Computer)
			_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}/{DIAL_UP_NETWORKING}"] = new VirtualEntry
			{
				Name = DIAL_UP_NETWORKING,
				Path = $"{DESKTOP}/{MY_COMPUTER}/{DIAL_UP_NETWORKING}",
				RealPath = null,
				Type = EntryType.SpecialFolder,
				IsVirtual = true,
				IconName = "dialup"
			};

			// Scheduled Tasks (inside My Computer)
			_virtualPaths[$"{DESKTOP}/{MY_COMPUTER}/{SCHEDULED_TASKS}"] = new VirtualEntry
			{
				Name = SCHEDULED_TASKS,
				Path = $"{DESKTOP}/{MY_COMPUTER}/{SCHEDULED_TASKS}",
				RealPath = null,
				Type = EntryType.SpecialFolder,
				IsVirtual = true,
				IconName = "tasks"
			};
		}

		// Initialize the recycled folder if it doesn't exist
		EnsureRecycleBinExists();

		// Make sure control panel has applets
		CreateControlPanelApplets();
	}

	private void EnsureRecycleBinExists()
	{
		var recycleBin = _virtualPaths[$"{DESKTOP}/{RECYCLE_BIN}"];
		if ( !string.IsNullOrEmpty( recycleBin.RealPath ) && !_realFileSystem.DirectoryExists( recycleBin.RealPath ) )
		{
			_realFileSystem.CreateDirectory( recycleBin.RealPath );

			// Create a readme explaining the recycle bin
			_realFileSystem.WriteAllText(
				Path.Combine( recycleBin.RealPath, "Readme.txt" ),
				"This folder represents the Recycle Bin.\nDeleted files would appear here in Windows 98."
			);
		}
	}

	private void CreateControlPanelApplets()
	{
		// Obsolete: This is a placeholder for creating control panel applets
	}

	/// <summary>
	/// Checks if the specified virtual path exists
	/// </summary>
	public bool PathExists( string virtualPath )
	{
		virtualPath = NormalizePath( virtualPath );

		// Check if it's a directly mapped virtual path
		if ( _virtualPaths.ContainsKey( virtualPath ) )
			return true;

		// Check if it's a real path that maps to a directory
		var entry = ResolveVirtualPath( virtualPath );
		if ( entry != null )
		{
			// If it's a virtual entry, it exists by definition
			if ( entry.IsVirtual )
				return true;

			// Otherwise check the real file system
			if ( entry.AssociatedFileSystem != null )
			{
				return entry.Type == EntryType.Directory
					? entry.AssociatedFileSystem.DirectoryExists( entry.RealPath )
					: entry.AssociatedFileSystem.FileExists( entry.RealPath );
			}
			else
			{
				return entry.Type == EntryType.Directory
					? _realFileSystem.DirectoryExists( entry.RealPath )
					: _realFileSystem.FileExists( entry.RealPath );
			}
		}

		return false;
	}

	/// <summary>
	/// Gets a virtual path from a real filesystem.data path
	/// </summary>
	public string GetVirtualPathFromRealPath( string realPath )
	{
		// Check if the real path is a direct mapping
		foreach ( var kv in _virtualPaths )
		{
			if ( kv.Value.RealPath == realPath )
				return kv.Key;
		}
		// try replace FakeSystemRoot with the virtual path
		if ( realPath.StartsWith( _rootPath ) )
		{
			string relativePath = realPath.Substring( _rootPath.Length ).TrimStart( Path.DirectorySeparatorChar );
			return $"{DESKTOP}/My Computer/C:{relativePath}";
		}
		return null;
	}


	/// <summary>
	/// Checks if the virtual path is a directory
	/// </summary>
	public bool IsDirectory( string virtualPath )
	{
		virtualPath = NormalizePath( virtualPath );

		var entry = ResolveVirtualPath( virtualPath );
		if ( entry == null )
			return false;

		return entry.Type == EntryType.Directory
			|| entry.Type == EntryType.SpecialFolder
			|| entry.Type == EntryType.Drive
			|| entry.Type == EntryType.ControlPanel;
	}

	/// <summary>
	/// Gets all directories at the specified virtual path
	/// </summary>
	public IEnumerable<VirtualEntry> GetDirectories( string virtualPath )
	{
		virtualPath = NormalizePath( virtualPath );

		// Try to use cache if available
		/*		if ( _cachedDirectoryContents.TryGetValue( virtualPath, out var cachedEntries ) )
				{
					return cachedEntries.Where( e =>
						e.Type == EntryType.Directory ||
						e.Type == EntryType.SpecialFolder ||
						e.Type == EntryType.Drive ||
						e.Type == EntryType.ControlPanel );
				}*/

		var results = new List<VirtualEntry>();
		var entry = ResolveVirtualPath( virtualPath );

		if ( entry == null )
			return results;

		// For fully virtual folders like My Computer, return predefined entries
		if ( entry.IsVirtual && entry.Type != EntryType.SpecialFolder && string.IsNullOrEmpty( entry.RealPath ) )
		{
			// For Control Panel, return applets
			if ( entry.Type == EntryType.ControlPanel )
			{
				// Get all control panel applets
				string controlPanelPrefix = $"{virtualPath}/";
				results.AddRange( _virtualPaths
					.Where( kv => kv.Key.StartsWith( controlPanelPrefix ) )
					.Select( kv => kv.Value ) );
			}
			// For other special virtual folders, handle them specially
			else
			{
				HandleSpecialVirtualFolder( virtualPath, results );
			}
		}
		// For special folders with real backing, combine real and virtual items
		else if ( entry.Type == EntryType.SpecialFolder || entry.Type == EntryType.Drive )
		{
			// Add predefined child virtual entries if they exist
			string prefix = $"{virtualPath}/";
			results.AddRange( _virtualPaths
				.Where( kv => kv.Key.StartsWith( prefix ) && kv.Key.Count( c => c == '/' ) == virtualPath.Count( c => c == '/' ) + 1 )
				.Select( kv => kv.Value ) );

			// Add real directories from the filesystem if this maps to a real path
			if ( !string.IsNullOrEmpty( entry.RealPath ) )
			{
				BaseFileSystem fs = entry.AssociatedFileSystem ?? _realFileSystem;

				try
				{
					foreach ( var dir in fs.FindDirectory( entry.RealPath ) )
					{
						string dirName = Path.GetFileName( dir );

						// Only add the real directory if there isn't already a virtual one with the same name
						if ( !results.Any( r => r.Name.Equals( dirName, StringComparison.OrdinalIgnoreCase ) ) )
						{
							results.Add( new VirtualEntry
							{
								Name = dirName,
								Path = $"{virtualPath}/{dirName}",
								RealPath = Path.Combine( entry.RealPath, dirName ).Replace( "\\", "/" ),
								Type = EntryType.Directory,
								IsVirtual = false,
								IconName = "folder",
								AssociatedFileSystem = entry.AssociatedFileSystem
							} );
						}
					}
				}
				catch ( Exception ex )
				{
					Log.Error( $"Error getting directories from {entry.RealPath}: {ex.Message}" );
				}
			}
		}
		// For real folders, return directories from the filesystem
		else if ( entry.Type == EntryType.Directory && !string.IsNullOrEmpty( entry.RealPath ) )
		{
			BaseFileSystem fs = entry.AssociatedFileSystem ?? _realFileSystem;

			try
			{
				foreach ( var dir in fs.FindDirectory( entry.RealPath ) )
				{
					string dirName = Path.GetFileName( dir );
					results.Add( new VirtualEntry
					{
						Name = dirName,
						Path = $"{virtualPath}/{dirName}",
						RealPath = Path.Combine( entry.RealPath, dirName ).Replace( "\\", "/" ),
						Type = EntryType.Directory,
						IsVirtual = false,
						IconName = "folder",
						AssociatedFileSystem = entry.AssociatedFileSystem
					} );
				}
			}
			catch ( Exception ex )
			{
				Log.Error( $"Error getting directories from {entry.RealPath}: {ex.Message}" );
			}
		}

		// Cache the results
		_cachedDirectoryContents[virtualPath] = results;

		return results.Where( e =>
			e.Type == EntryType.Directory ||
			e.Type == EntryType.SpecialFolder ||
			e.Type == EntryType.Drive ||
			e.Type == EntryType.ControlPanel ||
			e.Type == EntryType.ControlPanelApplet );
	}

	/// <summary>
	/// Gets all files at the specified virtual path
	/// </summary>
	public IEnumerable<VirtualEntry> GetFiles( string virtualPath )
	{
		virtualPath = NormalizePath( virtualPath );

		// Try to use cache if available
		/*if ( _cachedDirectoryContents.TryGetValue( virtualPath, out var cachedEntries ) )
		{
			return cachedEntries.Where( e =>
				e.Type == EntryType.File ||
				e.Type == EntryType.Shortcut ||
				e.Type == EntryType.ControlPanelApplet );
		}*/

		var results = new List<VirtualEntry>();
		var entry = ResolveVirtualPath( virtualPath );

		if ( entry == null )
			return results;

		// Control Panel applets are handled in GetDirectories
		if ( entry.Type == EntryType.ControlPanel )
		{
			return results; // Return empty list, applets are treated as folders
		}

		// For special folders, also add virtual files
		if ( entry.Type == EntryType.SpecialFolder || entry.Type == EntryType.Drive )
		{
			// Add predefined virtual files if they exist (like My Computer items)
			string prefix = $"{virtualPath}/";
			results.AddRange( _virtualPaths
				.Where( kv => kv.Key.StartsWith( prefix ) &&
					  kv.Key.Count( c => c == '/' ) == virtualPath.Count( c => c == '/' ) + 1 &&
					  (kv.Value.Type == EntryType.File || kv.Value.Type == EntryType.Shortcut) )
				.Select( kv => kv.Value ) );
		}

		// Also include real files if this has a real path
		if ( !string.IsNullOrEmpty( entry.RealPath ) &&
			(entry.Type == EntryType.Directory ||
			 entry.Type == EntryType.SpecialFolder ||
			 entry.Type == EntryType.Drive) )
		{
			BaseFileSystem fs = entry.AssociatedFileSystem ?? _realFileSystem;

			try
			{
				foreach ( var file in fs.FindFile( entry.RealPath ) )
				{
					string fileName = Path.GetFileName( file );
					string extension = Path.GetExtension( file ).ToLower();

					EntryType fileType = EntryType.File;
					string iconName = "file";

					// Check if it's a shortcut (lnk file)
					if ( extension == ".lnk" )
					{
						fileType = EntryType.Shortcut;
						iconName = "shortcut";
					}

					results.Add( new VirtualEntry
					{
						Name = fileName,
						Path = $"{virtualPath}/{fileName}",
						RealPath = Path.Combine( entry.RealPath, fileName ).Replace( "\\", "/" ),
						Type = fileType,
						IsVirtual = false,
						IconName = iconName,
						AssociatedFileSystem = entry.AssociatedFileSystem
					} );
				}
			}
			catch ( Exception ex )
			{
				Log.Error( $"Error getting files from {entry.RealPath}: {ex.Message}" );
			}
		}

		// Add the entries to our cache
		if ( !_cachedDirectoryContents.ContainsKey( virtualPath ) )
		{
			_cachedDirectoryContents[virtualPath] = results;
		}
		else
		{
			_cachedDirectoryContents[virtualPath].AddRange( results );
		}

		return results.Where( e =>
			e.Type == EntryType.File ||
			e.Type == EntryType.Shortcut ||
			e.Type == EntryType.ControlPanelApplet );
	}

	/// <summary>
	/// Gets all files and directories at the specified path
	/// </summary>
	public IEnumerable<VirtualEntry> GetDirectoryContents( string virtualPath )
	{
		var directories = GetDirectories( virtualPath );
		var files = GetFiles( virtualPath );
		return directories.Concat( files );
	}

	/// <summary>
	/// Gets a specific entry by its virtual path
	/// </summary>
	public VirtualEntry GetEntry( string virtualPath )
	{
		virtualPath = NormalizePath( virtualPath );
		return ResolveVirtualPath( virtualPath );
	}

	/// <summary>
	/// Resolves a virtual path to its corresponding VirtualEntry
	/// </summary>
	private VirtualEntry ResolveVirtualPath( string virtualPath )
	{
		// Support Windows-style drive letter paths (C:/..., D:\..., etc)
		if ( !string.IsNullOrEmpty( virtualPath ) &&
			virtualPath.Length >= 2 &&
			char.IsLetter( virtualPath[0] ) &&
			virtualPath[1] == ':' )
		{
			// Normalize slashes
			virtualPath = virtualPath.Replace( '\\', '/' );

			// Remove any leading slash after the drive letter
			string rest = virtualPath.Substring( 2 );
			if ( rest.StartsWith( "/" ) ) rest = rest.Substring( 1 );

			// Compose the virtual path in the VFS format
			virtualPath = $"{DESKTOP}/{MY_COMPUTER}/{virtualPath[0]}:/{rest}";
		}

		// Check if this path is directly in our virtual path mapping
		if ( _virtualPaths.TryGetValue( virtualPath, out var entry ) )
			return entry;

		// If not a direct mapping, parse the path to find the nearest parent virtual path
		string[] pathParts = virtualPath.Split( '/' );

		// Start from the full path and work backwards
		for ( int i = pathParts.Length - 1; i >= 0; i-- )
		{
			string parentPath = string.Join( "/", pathParts.Take( i + 1 ) );

			if ( _virtualPaths.TryGetValue( parentPath, out var parentEntry ) )
			{
				// If we found a parent entry and it has a real path, build the rest of the path
				if ( !string.IsNullOrEmpty( parentEntry.RealPath ) && !parentEntry.IsVirtual )
				{
					string remainingPath = string.Join( "/", pathParts.Skip( i + 1 ) );
					string resolvedRealPath = Path.Combine( parentEntry.RealPath, remainingPath ).Replace( "\\", "/" );

					// Determine if it's a file or directory
					BaseFileSystem fs = parentEntry.AssociatedFileSystem ?? _realFileSystem;
					bool isDirectory = fs.DirectoryExists( resolvedRealPath );
					bool isFile = !isDirectory && fs.FileExists( resolvedRealPath );

					if ( !isDirectory && !isFile )
						return null; // Path doesn't exist

					return new VirtualEntry
					{
						Name = Path.GetFileName( virtualPath ),
						Path = virtualPath,
						RealPath = resolvedRealPath,
						Type = isDirectory ? EntryType.Directory : EntryType.File,
						IsVirtual = false,
						IconName = isDirectory ? "folder" : "file",
						AssociatedFileSystem = parentEntry.AssociatedFileSystem
					};
				}

				// For virtual parents without real paths, check if this is a special case
				return HandleSpecialVirtualPathResolution( virtualPath, parentEntry, pathParts.Skip( i + 1 ).ToArray() );
			}
		}

		return null; // Path could not be resolved
	}

	/// <summary>
	/// Handles special virtual paths that need custom resolution logic
	/// </summary>
	private VirtualEntry HandleSpecialVirtualPathResolution( string virtualPath, VirtualEntry parentEntry, string[] remainingPathParts )
	{
		// No remaining parts means we're asking for the parent itself
		if ( remainingPathParts.Length == 0 )
			return parentEntry;

		// Handle special cases based on the parent type
		switch ( parentEntry.Type )
		{
			// Control Panel has "applets" as its items
			case EntryType.ControlPanel:
				string appletName = remainingPathParts[0];
				string appletPath = $"{parentEntry.Path}/{appletName}";

				if ( _virtualPaths.TryGetValue( appletPath, out var applet ) )
					return applet;
				break;

				// Other special cases can be handled here
		}

		return null;
	}

	/// <summary>
	/// Handles populating special virtual folders like My Computer
	/// </summary>
	private void HandleSpecialVirtualFolder( string virtualPath, List<VirtualEntry> results )
	{
		// For My Computer, add drive letters and special folders
		if ( virtualPath == $"{DESKTOP}/{MY_COMPUTER}" )
		{
			string prefix = $"{virtualPath}/";
			results.AddRange( _virtualPaths
				.Where( kv => kv.Key.StartsWith( prefix ) && kv.Key.Count( c => c == '/' ) == virtualPath.Count( c => c == '/' ) + 1 )
				.Select( kv => kv.Value ) );
		}
	}

	/// <summary>
	/// Normalizes a path to use forward slashes and remove any leading/trailing slashes
	/// </summary>
	private string NormalizePath( string path )
	{
		// Replace backslashes with forward slashes
		path = path.Replace( "\\", "/" );

		// Remove leading slash if present
		if ( path.StartsWith( "/" ) )
			path = path.Substring( 1 );

		// Remove trailing slash if present
		if ( path.EndsWith( "/" ) && path.Length > 1 )
			path = path.Substring( 0, path.Length - 1 );

		// Special case: if path is empty, return Desktop
		if ( string.IsNullOrEmpty( path ) )
			return DESKTOP;

		return path;
	}

	/// <summary>
	/// Clear the cached directory listings (use when files change)
	/// </summary>
	public void ClearCache()
	{
		_cachedDirectoryContents.Clear();
	}

	/// <summary>
	/// Creates a shortcut file pointing to a target
	/// </summary>
	public void CreateShortcut( string shortcutPath, string targetPath, string iconName = null, string arguments = "", string workingDir = "" )
	{
		// Ensure directory exists
		string directory = Path.GetDirectoryName( shortcutPath );
		if ( !_realFileSystem.DirectoryExists( directory ) )
		{
			_realFileSystem.CreateDirectory( directory );
		}

		// Create the shortcut descriptor
		var shortcut = new ShortcutDescriptor( targetPath,
			string.IsNullOrEmpty( workingDir ) ? Path.GetDirectoryName( targetPath ) : workingDir,
			arguments, iconName );

		// Write to file
		_realFileSystem.WriteAllText( shortcutPath, shortcut.ToFileContent() );
	}

	/// <summary>
	/// Reads a shortcut file and returns the ShortcutDescriptor
	/// </summary>
	public ShortcutDescriptor GetShortcutFromFile( string path )
	{
		if ( !_realFileSystem.FileExists( path ) )
			return null;

		try
		{
			string content = _realFileSystem.ReadAllText( path );
			return ShortcutDescriptor.FromFileContent( content );
		}
		catch ( Exception ex )
		{
			Log.Error( $"Failed to read shortcut file '{path}': {ex.Message}" );
			return null;
		}
	}

	/// <summary>
	/// Resolves a program path from a given name
	/// </summary>
	/// <param name="programName"></param>
	/// <returns></returns>
	public string ResolveProgramPath( string programName )
	{
		// If it's a full path, just return it
		if ( programName.Contains( "/" ) || programName.Contains( "\\" ) )
		{
			return programName;
		}

		// Check Windows directory
		string windowsPath = $"FakeSystemRoot/Windows/{programName}";
		if ( _realFileSystem.FileExists( windowsPath ) )
		{
			return windowsPath;
		}

		// Check Program Files directory structure
		string programFilesPath = $"FakeSystemRoot/Program Files";
		foreach ( var dir in _realFileSystem.FindDirectory( programFilesPath ) )
		{
			string progPath = $"{dir}/{programName}";
			if ( _realFileSystem.FileExists( progPath ) )
			{
				return progPath;
			}
		}

		// If it doesn't have .exe, try adding it
		if ( !programName.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
		{
			return ResolveProgramPath( programName + ".exe" );
		}

		return null;
	}

	/// <summary>
	/// Registers a file association for a specific file extension
	/// </summary>
	/// <param name="association"></param>
	public void RegisterFileAssociation( FileAssociation association )
	{
		_fileAssociations[association.Extension] = association;
	}

	public FileAssociation GetFileAssociation( string extension )
	{
		if ( string.IsNullOrEmpty( extension ) )
			return null;

		// Make sure the extension starts with a dot
		if ( !extension.StartsWith( "." ) )
			extension = "." + extension;

		if ( _fileAssociations.TryGetValue( extension, out var association ) )
			return association;

		return null;
	}

	/// <summary>
	/// Opens a file using the registered file association
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	public bool OpenFile( string filePath )
	{
		string extension = Path.GetExtension( filePath );
		var association = GetFileAssociation( extension );

		if ( association != null )
		{
			return association.Execute( filePath, this );
		}

		// No association found
		Log.Warning( $"No file association found for {extension} files" );
		return false;
	}

	/// <summary>
	/// Resolves and executes a shortcut
	/// </summary>
	public bool ResolveShortcut( string shortcutPath )
	{
		var shortcut = GetShortcutFromFile( shortcutPath );
		if ( shortcut == null )
			return false;

		return shortcut.Resolve();
	}

	/// <summary>
	/// Represents the type of entry in the virtual file system
	/// </summary>
	public enum EntryType
	{
		File,
		Directory,
		SpecialFolder,
		Drive,
		Shortcut,
		ControlPanel,
		ControlPanelApplet
	}

	/// <summary>
	/// Represents an entry in the virtual file system
	/// </summary>
	public class VirtualEntry
	{
		/// <summary>
		/// The name of the entry
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The full virtual path to the entry
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// The corresponding real path, if any
		/// </summary>
		public string RealPath { get; set; }

		/// <summary>
		/// The type of entry
		/// </summary>
		public EntryType Type { get; set; }

		/// <summary>
		/// Whether this is a fully virtual entry (no real counterpart)
		/// </summary>
		public bool IsVirtual { get; set; }

		/// <summary>
		/// The base name of the icon to use for this entry
		/// </summary>
		public string IconName { get; set; }

		/// <summary>
		/// The associated file system for entries that use a different BaseFileSystem
		/// </summary>
		public BaseFileSystem AssociatedFileSystem { get; set; }
	}
}
