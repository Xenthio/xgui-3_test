using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.User;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FakeOperatingSystem.Shell;

/// <summary>
/// Manages the shell namespace hierarchy (special folders, virtual folders, etc.)
/// </summary>
public class ShellNamespace
{
	// Singleton instance
	public static ShellNamespace Instance { get; private set; }

	// Constants for special folder names
	public const string DESKTOP = "Desktop";
	public const string MY_COMPUTER = "My Computer";
	public const string MY_DOCUMENTS = "My Documents";
	public const string RECYCLE_BIN = "Recycle Bin";
	public const string NETWORK_NEIGHBORHOOD = "Network Neighborhood";
	public const string CONTROL_PANEL = "Control Panel";
	public const string PRINTERS = "Printers";
	public const string DIAL_UP_NETWORKING = "Dial-Up Networking";
	public const string SCHEDULED_TASKS = "Scheduled Tasks";

	// The core VFS for file operations
	private IVirtualFileSystem _vfs;

	// Registry of shell folders
	private Dictionary<string, ShellFolder> _shellFolders = new();

	public ShellNamespace( IVirtualFileSystem vfs )
	{
		Instance = this;
		_vfs = vfs;

		// Set up the shell namespace hierarchy
		InitializeNamespace();
	}

	private void InitializeNamespace()
	{
		// Desktop (root of shell namespace)
		RegisterShellFolder( new ShellFolder
		{
			Name = DESKTOP,
			Path = DESKTOP,
			RealPath = UserProfileHelper.GetDesktopPath(),
			Type = ShellFolderType.SpecialFolder,
			IconName = "desktop"
		} );

		// My Computer
		RegisterShellFolder( new ShellFolder
		{
			Name = MY_COMPUTER,
			Path = $"{DESKTOP}/{MY_COMPUTER}",
			Type = ShellFolderType.SpecialFolder,
			IconName = "mycomputer",
			IsVirtual = true
		} );

		// My Documents
		RegisterShellFolder( new ShellFolder
		{
			Name = MY_DOCUMENTS,
			Path = $"{DESKTOP}/{MY_DOCUMENTS}",
			RealPath = UserProfileHelper.GetMyDocumentsPath(),
			Type = ShellFolderType.SpecialFolder,
			IconName = "mydocuments"
		} );

		// Recycle Bin
		RegisterShellFolder( new ShellFolder
		{
			Name = RECYCLE_BIN,
			Path = $"{DESKTOP}/{RECYCLE_BIN}",
			RealPath = "C:/Recycled", // Assuming Recycled is the VFS path for the recycle bin
			Type = ShellFolderType.SpecialFolder,
			IconName = "recyclebinempty"
		} );

		// Network Neighborhood
		RegisterShellFolder( new ShellFolder
		{
			Name = NETWORK_NEIGHBORHOOD,
			Path = $"{DESKTOP}/{NETWORK_NEIGHBORHOOD}",
			Type = ShellFolderType.SpecialFolder,
			IconName = "networkneighborhood",
			IsVirtual = true
		} );

		// Mount drives in My Computer
		RegisterShellFolder( new ShellFolder
		{
			Name = "(C:)",
			Path = $"{DESKTOP}/{MY_COMPUTER}/C:",
			RealPath = "C:",
			Type = ShellFolderType.Drive,
			IconName = "drive"
		} );

		RegisterShellFolder( new ShellFolder
		{
			Name = "Mounted (FS:A:)",
			Path = $"{DESKTOP}/{MY_COMPUTER}/FS:A:",
			RealPath = "FS:A:",
			Type = ShellFolderType.Drive,
			IconName = "drive"
		} );

		RegisterShellFolder( new ShellFolder
		{
			Name = "OrgData (FS:B:)",
			Path = $"{DESKTOP}/{MY_COMPUTER}/FS:B:",
			RealPath = "FS:B:",
			Type = ShellFolderType.Drive,
			IconName = "drive"
		} );

		RegisterShellFolder( new ShellFolder
		{
			Name = "Data (FS:C:)",
			Path = $"{DESKTOP}/{MY_COMPUTER}/FS:C:",
			RealPath = "FS:C:",
			Type = ShellFolderType.Drive,
			IconName = "drive"
		} );

		// Control Panel
		RegisterShellFolder( new ShellFolder
		{
			Name = CONTROL_PANEL,
			Path = $"{DESKTOP}/{MY_COMPUTER}/{CONTROL_PANEL}",
			Type = ShellFolderType.ControlPanel,
			IconName = "controlpanel",
			IsVirtual = true
		} );

		// Printers
		RegisterShellFolder( new ShellFolder
		{
			Name = "Printers",
			Path = $"{DESKTOP}/{MY_COMPUTER}/{PRINTERS}",
			Type = ShellFolderType.ControlPanel,
			IconName = "printer",
			IsVirtual = true
		} );

		// Dial-Up Networking
		RegisterShellFolder( new ShellFolder
		{
			Name = "Dial-Up Networking",
			Path = $"{DESKTOP}/{MY_COMPUTER}/{DIAL_UP_NETWORKING}",
			Type = ShellFolderType.ControlPanel,
			IconName = "dialup",
			IsVirtual = true
		} );

		// Scheduled Tasks
		RegisterShellFolder( new ShellFolder
		{
			Name = SCHEDULED_TASKS,
			Path = $"{DESKTOP}/{MY_COMPUTER}/{SCHEDULED_TASKS}",
			Type = ShellFolderType.ControlPanel,
			IconName = "tasks",
			IsVirtual = true
		} );

		// Add Control Panel applets
		AddControlPanelApplets();
	}

	private void AddControlPanelApplets()
	{
		var applets = new[]
		{
			 ("Network", "network"),
			 ("System", "system"),
			 ("Mouse", "mouse"),
			 ("Keyboard", "keyboard"),
			 ("Sounds", "sounds"),
			 ("Add/Remove Programs", "addremove"),
			 ("Date/Time", "datetime"),
			 ("Regional Settings", "regional")
		};

		string controlPanelPath = $"{DESKTOP}/{MY_COMPUTER}/{CONTROL_PANEL}";

		var displayApplet = new DesktopSettingsApplet();

		RegisterShellFolder( new ShellFolder
		{
			Name = displayApplet.Name,
			Path = $"{controlPanelPath}/{displayApplet.Name}",
			Type = ShellFolderType.ControlPanelApplet,
			IconName = displayApplet.IconName,
			IsVirtual = true,
			Applet = displayApplet
		} );

		foreach ( var (name, icon) in applets )
		{
			RegisterShellFolder( new ShellFolder
			{
				Name = name,
				Path = $"{controlPanelPath}/{name}",
				Type = ShellFolderType.ControlPanelApplet,
				IconName = icon,
				IsVirtual = true
			} );
		}
	}

	public void RegisterShellFolder( ShellFolder folder )
	{
		_shellFolders[folder.Path] = folder;
	}

	/// <summary>
	/// Gets a shell folder by its path
	/// </summary>
	public ShellFolder GetFolder( string path )
	{
		if ( string.IsNullOrEmpty( path ) )
		{
			if ( _shellFolders.TryGetValue( DESKTOP, out var desktopFolder ) )
				return desktopFolder;
			return null;
		}

		path = NormalizePath( path );

		if ( _shellFolders.TryGetValue( path, out var folder ) )
			return folder;

		// Look for a drive entry whose Path is a prefix of the requested path
		foreach ( var drive in _shellFolders.Values.Where( f => f.Type == ShellFolderType.Drive ) )
		{
			// Ensure trailing slash for correct prefix matching
			string drivePrefix = drive.Path.EndsWith( "/" ) ? drive.Path : drive.Path + "/";
			if ( path.StartsWith( drivePrefix, StringComparison.OrdinalIgnoreCase ) )
			{
				// Get the subpath after the drive
				string subPath = path.Substring( drivePrefix.Length );
				string vfsPath = string.IsNullOrEmpty( subPath ) ? drive.RealPath : Path.Combine( drive.RealPath, subPath ).Replace( '\\', '/' );

				if ( _vfs.DirectoryExists( vfsPath ) )
				{
					return new ShellFolder
					{
						Name = _vfs.GetFileName( vfsPath ),
						Path = path,
						RealPath = vfsPath,
						Type = ShellFolderType.Directory,
						IconName = "folder"
					};
				}
			}
		}

		// Fallback: treat as a VFS path directly if it's an absolute path
		if ( path.Contains( ":/" ) && _vfs.DirectoryExists( path ) )
		{
			return new ShellFolder
			{
				Name = _vfs.GetFileName( path ),
				Path = path,
				RealPath = path,
				Type = ShellFolderType.Directory,
				IconName = "folder"
			};
		}

		return null;
	}

	/// <summary>
	/// Gets all child items (files and folders) in a shell folder
	/// </summary>
	public IEnumerable<ShellItem> GetItems( string folderPath )
	{
		var items = new List<ShellItem>();
		var folder = GetFolder( folderPath );

		if ( folder == null )
			return items;

		// Add registered subfolders that are direct children
		string prefix = folder.Path.EndsWith( "/" ) ? folder.Path : $"{folder.Path}/";
		if ( folder.Path == DESKTOP && !prefix.EndsWith( "/" ) ) prefix += "/";

		foreach ( var sfPath in _shellFolders.Keys )
		{
			var sf = _shellFolders[sfPath];
			if ( sf.Path.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) && sf.Path.Length > prefix.Length )
			{
				string remainingPath = sf.Path.Substring( prefix.Length );
				if ( !remainingPath.Contains( '/' ) )
				{
					items.Add( new ShellItem
					{
						Name = sf.Name,
						Path = sf.Path,
						RealPath = sf.RealPath,
						IsFolder = true,
						IconName = sf.IconName,
						Type = sf.Type
					} );
				}
			}
		}

		// For non-virtual folders, add real files and folders from the VFS
		if ( !folder.IsVirtual && !string.IsNullOrEmpty( folder.RealPath ) && _vfs.DirectoryExists( folder.RealPath ) )
		{
			// Add directories
			foreach ( var dir in _vfs.GetDirectories( folder.RealPath ) )
			{
				string dirName = _vfs.GetFileName( dir );

				// DO NOT ADD "." AND ".." DIRECTORIES
				if ( dirName == "." || dirName == ".." )
					continue;

				// Skip if this directory is already added as a special folder
				if ( items.Any( i => i.IsFolder && i.Name.Equals( dirName, StringComparison.OrdinalIgnoreCase ) && i.RealPath == dir ) )
					continue;

				// Also skip if a registered shell folder has this RealPath
				if ( _shellFolders.Values.Any( sf => sf.RealPath == dir ) )
					continue;

				items.Add( new ShellItem
				{
					Name = dirName,
					Path = $"{folder.Path}/{dirName}",
					RealPath = dir,
					IsFolder = true,
					IconName = "folder",
					Type = ShellFolderType.Directory
				} );
			}

			// Add files
			foreach ( var file in _vfs.GetFiles( folder.RealPath ) )
			{
				string fileName = _vfs.GetFileName( file );
				string extension = _vfs.GetExtension( file ).ToLowerInvariant();

				string iconName = "file";
				ShellFolderType type = ShellFolderType.File;

				// Handle shortcuts specially
				if ( extension == ".lnk" )
				{
					iconName = "shortcut";
					type = ShellFolderType.Shortcut;
				}

				items.Add( new ShellItem
				{
					Name = fileName,
					Path = $"{folder.Path}/{fileName}",
					RealPath = file,
					IsFolder = false,
					IconName = iconName,
					Type = type
				} );
			}
		}

		return items.DistinctBy( i => i.Path ).ToList();
	}

	/// <summary>
	/// Normalizes a shell path
	/// </summary>
	private string NormalizePath( string path )
	{
		if ( string.IsNullOrEmpty( path ) ) return path;
		path = path.Replace( '\\', '/' );

		if ( path == DESKTOP ) return DESKTOP;

		if ( path.StartsWith( "/" ) && path.Length > 1 )
			path = path.Substring( 1 );

		if ( path.EndsWith( "/" ) && path.Length > 1 )
			path = path.Substring( 0, path.Length - 1 );

		return path;
	}
}

/// <summary>
/// The type of a shell folder
/// </summary>
public enum ShellFolderType
{
	Directory,
	File,
	SpecialFolder,
	Drive,
	Shortcut,
	ControlPanel,
	ControlPanelApplet
}

/// <summary>
/// Represents a special folder in the shell namespace
/// </summary>
public class ShellFolder
{
	/// <summary>
	/// Display name of the folder
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Shell namespace path
	/// </summary>
	public string Path { get; set; }

	/// <summary>
	/// Real path in the VFS, if any
	/// </summary>
	public string RealPath { get; set; }

	/// <summary>
	/// Type of folder
	/// </summary>
	public ShellFolderType Type { get; set; }

	/// <summary>
	/// Whether this is a virtual folder with no real files
	/// </summary>
	public bool IsVirtual { get; set; }

	/// <summary>
	/// Icon name for display
	/// </summary>
	public string IconName { get; set; }

	/// <summary>
	/// Launches the folder if it is a control panel applet
	/// </summary>
	public IControlPanelApplet Applet { get; set; }
}

/// <summary>
/// Represents an item in the shell namespace
/// </summary>
public class ShellItem
{
	/// <summary>
	/// Display name of the item
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Shell namespace path
	/// </summary>
	public string Path { get; set; }

	/// <summary>
	/// Real path in the VFS, if any
	/// </summary>
	public string RealPath { get; set; }

	/// <summary>
	/// Whether this is a folder or file
	/// </summary>
	public bool IsFolder { get; set; }

	/// <summary>
	/// Type of folder/file
	/// </summary>
	public ShellFolderType Type { get; set; }

	/// <summary>
	/// Icon name for display
	/// </summary>
	public string IconName { get; set; }
}

public interface IControlPanelApplet
{
	string Name { get; }
	string IconName { get; }
	string Description { get; }
	void Launch();
}
