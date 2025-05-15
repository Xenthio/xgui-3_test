using FakeOperatingSystem;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using XGUI;

namespace FakeDesktop;

/// <summary>
/// Extension of FileBrowserView that works with both BaseFileSystem and VirtualFileSystem
/// </summary>
public class VirtualFileBrowserView : FileBrowserView
{
	// The virtual file system instance
	private VirtualFileSystem _virtualFileSystem;

	// Track if we're using virtual mode or regular mode
	private bool _usingVirtualMode = false;

	// Current virtual path (when in virtual mode)
	private string _currentVirtualPath = VirtualFileSystem.DESKTOP;

	// Navigation history
	private List<string> _navigationHistory = new();
	private int _historyIndex = -1;

	public VirtualFileBrowserView() : base()
	{
		// Override the DirectoryOpened event to handle virtual navigation
		OnDirectoryOpened += HandleDirectoryOpened;
		OnFileOpened += HandleFileOpened;
	}

	/// <summary>
	/// Initialize with a virtual file system
	/// </summary>
	public void Initialize( VirtualFileSystem virtualFileSystem, BaseFileSystem defaultFileSystem )
	{
		_virtualFileSystem = virtualFileSystem;
		base.CurrentFileSystem = defaultFileSystem;

		// Navigate to Desktop (root of the virtual file system)
		NavigateToVirtualPath( VirtualFileSystem.DESKTOP, sound: false );
	}

	/// <summary>
	/// Navigate to a virtual path
	/// </summary>
	public void NavigateToVirtualPath( string virtualPath, bool sound = true )
	{
		if ( _virtualFileSystem == null )
			return;

		// Save to history
		if ( _historyIndex >= 0 && _historyIndex < _navigationHistory.Count - 1 )
		{
			// If navigating from middle of history, truncate forward items
			_navigationHistory.RemoveRange( _historyIndex + 1, _navigationHistory.Count - _historyIndex - 1 );
		}
		_navigationHistory.Add( virtualPath );
		_historyIndex = _navigationHistory.Count - 1;

		// Update current path tracking
		_currentVirtualPath = virtualPath;
		_usingVirtualMode = true;

		// Get the virtual entry
		var entry = _virtualFileSystem.GetEntry( virtualPath );
		if ( entry == null )
		{
			Log.Warning( $"Virtual path not found: {virtualPath}" );
			return;
		}

		if ( sound ) PlaySingleClickSound();
		_currentContextMenu?.Delete();

		// Clear existing items
		ListView.Items.Clear();
		FileItems.Clear();

		// Properly clean up UI
		ListView.UpdateItems();

		// Update base class CurrentPath for proper event handling
		if ( !string.IsNullOrEmpty( entry.RealPath ) )
		{
			// If entry has a real path, set the file system and path
			base.CurrentFileSystem = entry.AssociatedFileSystem ?? FileSystem.Data;
			base.CurrentPath = entry.RealPath;
		}
		else
		{
			// For purely virtual entries, we'll use a placeholder path
			base.CurrentPath = "virtual:" + virtualPath;
		}

		// Trigger navigation event
		RaiseNavigateToEvent( _currentVirtualPath );

		// Add directory contents
		PopulateFromVirtualPath( virtualPath );
	}

	/// <summary>
	/// Navigate back in history
	/// </summary>
	public void GoBack()
	{
		if ( _historyIndex > 0 )
		{
			_historyIndex--;
			NavigateToVirtualPath( _navigationHistory[_historyIndex] );
		}
	}

	/// <summary>
	/// Navigate forward in history
	/// </summary>
	public void GoForward()
	{
		if ( _historyIndex < _navigationHistory.Count - 1 )
		{
			_historyIndex++;
			NavigateToVirtualPath( _navigationHistory[_historyIndex] );
		}
	}

	/// <summary>
	/// Navigate to parent folder
	/// </summary>
	public void GoUp()
	{
		if ( string.IsNullOrEmpty( _currentVirtualPath ) || _currentVirtualPath == VirtualFileSystem.DESKTOP )
			return;

		int lastSlash = _currentVirtualPath.LastIndexOf( '/' );
		if ( lastSlash > 0 )
		{
			string parentPath = _currentVirtualPath.Substring( 0, lastSlash );
			NavigateToVirtualPath( parentPath );
		}
		else
		{
			// If no slashes, go to root Desktop
			NavigateToVirtualPath( VirtualFileSystem.DESKTOP, sound: false );
		}
	}

	public bool OpenDirectoryEnabled = true;
	public bool OpenFileEnabled = true;

	/// <summary>
	/// Handle directory opened event to trigger virtual navigation if needed
	/// </summary>
	public virtual void HandleDirectoryOpened( string path )
	{
		if ( !OpenDirectoryEnabled )
			return;

		PlayDoubleClickSoundNoFirstClick();
		if ( _usingVirtualMode )
		{
			// Check if this is one of our virtual items
			foreach ( var item in FileItems )
			{
				if ( item.FullPath == path && item.IsDirectory )
				{
					// This is a virtual path, navigate to it
					NavigateToVirtualPath( path );
					return;
				}
			}
		}
	}

	/// <summary>
	/// Handle file opened event
	/// </summary>
	public virtual void HandleFileOpened( string path )
	{
		if ( !OpenFileEnabled )
			return;

		// doubleclick sound
		PlayDoubleClickSound();

		if ( _usingVirtualMode )
		{
			var entry = _virtualFileSystem.GetEntry( path );
			if ( entry != null )
			{
				// Special handling for shortcuts first
				if ( entry.Name.EndsWith( ".lnk", StringComparison.OrdinalIgnoreCase ) )
				{
					Log.Info( $"Opening shortcut: {entry.Name}" );
					if ( !_virtualFileSystem.ResolveShortcut( entry.RealPath ) )
					{
						Log.Warning( $"Failed to resolve shortcut: {entry.Name}" );
					}
					return;
				}

				// Control panel applets
				if ( entry.Type == VirtualFileSystem.EntryType.ControlPanelApplet )
				{
					Log.Info( $"Opening Control Panel applet: {entry.Name}" );
					// TODO: Launch appropriate control panel window
					return;
				}

				// EXE files are launched directly using the new process manager
				if ( entry.Name.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
				{
					Log.Info( $"Launching executable: {entry.Name}" );
					// You can extend Win32LaunchOptions as needed
					var launchOptions = new Win32LaunchOptions
					{
						Arguments = "", // Optionally parse arguments
						WorkingDirectory = System.IO.Path.GetDirectoryName( entry.RealPath )
					};
					ProcessManager.Instance?.OpenExecutable( entry.Path, launchOptions );
					return;
				}

				// For other files, use file associations
				if ( _virtualFileSystem.OpenFile( entry.RealPath ) )
				{
					return;
				}

				// If all else fails, just log that we're opening the file
				Log.Info( $"Opening file with no association: {entry.Name}" );
			}
		}
	}

	async void PlayDoubleClickSoundNoFirstClick()
	{
		//PlaySingleClickSound();
		await GameTask.DelaySeconds( 0.16f );
		PlaySingleClickSound();
	}
	async void PlayDoubleClickSound()
	{
		PlaySingleClickSound();
		await GameTask.DelaySeconds( 0.16f );
		PlaySingleClickSound();
	}

	async void PlaySingleClickSound()
	{
		var soundpath = XGUISoundSystem.GetSound( "START" );
		var soundfile = SoundFile.Load( soundpath );
		Sound.PlayFile( soundfile );
		await GameTask.DelaySeconds( 0.08f );
		Sound.PlayFile( soundfile );
	}

	/// <summary>
	/// Populate the browser view from a virtual path
	/// </summary>
	private void PopulateFromVirtualPath( string virtualPath )
	{
		// Get all directory contents
		var contents = _virtualFileSystem.GetDirectoryContents( virtualPath );

		// Process directories first
		foreach ( var entry in contents.Where( e =>
			e.Type == VirtualFileSystem.EntryType.Directory ||
			e.Type == VirtualFileSystem.EntryType.SpecialFolder ||
			e.Type == VirtualFileSystem.EntryType.Drive ||
			e.Type == VirtualFileSystem.EntryType.ControlPanel ) )
		{
			AddDirectoryToView( entry.Path, true, entry.Name );

			// Update the icon for this item
			UpdateItemIcon( entry.Path, entry.IconName, true );
		}

		// Then process files
		foreach ( var entry in contents.Where( e =>
			e.Type == VirtualFileSystem.EntryType.File ||
			e.Type == VirtualFileSystem.EntryType.Shortcut ||
			e.Type == VirtualFileSystem.EntryType.ControlPanelApplet ) )
		{
			AddFileToView( entry.Path, true, entry.Name );

			// Update the icon for this item
			UpdateItemIcon( entry.Path, entry.IconName, false );
		}
	}

	/// <summary>
	/// Update the icon for a specific item
	/// </summary>
	private void UpdateItemIcon( string path, string iconName, bool isDirectory )
	{
		// Find the corresponding ListView item
		var fileItem = FileItems.FirstOrDefault( item => item.FullPath == path );
		if ( fileItem == null )
			return;

		foreach ( var listViewItem in ListView.Items )
		{
			if ( listViewItem.Data == fileItem && listViewItem.IconPanel != null )
			{
				var iconPanel = listViewItem.IconPanel;
				var size = ViewMode == FileBrowserViewMode.Icons ? 32 : 16;

				// Use custom icon from desktop.ini if present
				if ( isDirectory )
				{
					string customIcon = FileIconHelper.GetCustomFolderIconFromDesktopIni( path, _virtualFileSystem );
					if ( !string.IsNullOrEmpty( customIcon ) )
					{
						iconPanel.SetFolderIcon( customIcon, size );
						break;
					}
					if ( string.IsNullOrEmpty( iconName ) || iconName == "folder" )
					{
						iconPanel.SetFolderIcon( "folder", size );
					}
					else
					{
						iconPanel.SetIcon( iconName, XGUIIconSystem.IconType.Folder, size );
					}
				}
				else
				{
					// For files, try to determine by extension
					string extension = System.IO.Path.GetExtension( path );
					if ( !string.IsNullOrEmpty( extension ) && extension.StartsWith( "." ) )
					{
						extension = extension.Substring( 1 );
					}

					if ( extension == "exe" )
					{
						// todo, lookup icon inside of exe.
						var filename = System.IO.Path.GetFileNameWithoutExtension( path );
						iconPanel.SetFileIcon( $"exe_{filename}", size );
						return;
					}

					if ( string.IsNullOrEmpty( iconName ) || iconName == "file" )
					{
						iconPanel.SetFileIcon( extension, size );
					}
					else
					{
						iconPanel.SetIcon( iconName, XGUIIconSystem.IconType.FileType, size );
					}
				}
				break;
			}
		}
	}

	protected override void OnClick( MousePanelEvent e )
	{
		_currentContextMenu?.Delete();
	}
	ContextMenu _currentContextMenu;
	protected override void OnRightClick( MousePanelEvent e )
	{
		base.OnRightClick( e );

		// Determine if the click was on an item by checking if any ListView items are hovered
		bool clickedOnItem = false;
		foreach ( var item in ListView.Items )
		{
			if ( item.HasHovered )
			{
				clickedOnItem = true;
				ShowItemContextMenu( FileItems.FirstOrDefault( fi => fi == item.Data ), e );
				ListView.SelectItem( item );
				break;
			}
		}

		if ( !clickedOnItem )
		{
			ShowEmptySpaceContextMenu( e );
		}
	}

	private void ShowEmptySpaceContextMenu( MousePanelEvent e )
	{
		_currentContextMenu?.Delete();
		_currentContextMenu = new ContextMenu( this, ContextMenu.PositionMode.UnderMouse );

		_currentContextMenu.AddMenuItem( "Refresh", () =>
		{
			Refresh();
			_currentContextMenu?.Delete();
		} );

		_currentContextMenu.AddSeparator();

		// "New" submenu
		_currentContextMenu.AddSubmenuItem( "New", submenu =>
		{
			// Get all registered file associations (file types)
			var associations = FileAssociation.Associations;

			if ( associations != null )
			{
				submenu.AddMenuItem( "Folder", () =>
				{
					string newFolderName = "New Folder";

					string basePath = _currentVirtualPath;
					var entry = _virtualFileSystem.GetEntry( basePath );
					var fs = entry.AssociatedFileSystem ?? FileSystem.Data;

					string newFolderPath = $"{entry.RealPath}/{newFolderName}";

					// Ensure unique folder name
					int count = 1;
					while ( fs.DirectoryExists( newFolderPath ) )
					{
						newFolderName = $"New Folder ({count})";
						newFolderPath = $"{_currentVirtualPath}/{newFolderName}";
						count++;
					}
					fs.CreateDirectory( newFolderPath );
					Refresh();
					_currentContextMenu?.Delete();
				} );

				submenu.AddSeparator();

				// Sort by friendly name for a nice menu
				foreach ( var assoc in associations.Values.OrderBy( a => a?.Actions?.FirstOrDefault().Value?.DisplayName ?? a.ToString() ) )
				{
					if ( !assoc.ShouldShowInShellCreateNew )
						continue;
					// Use the extension as the file type, e.g., "Text Document"
					string ext = assoc.Extension;
					string friendlyName = assoc.FriendlyName ?? ext.ToUpper() + " File";
					string iconName = assoc.IconName ?? "";

					submenu.AddMenuItem( friendlyName, () =>
					{
						string basePath = _currentVirtualPath;
						var entry = _virtualFileSystem.GetEntry( basePath );
						var fs = entry.AssociatedFileSystem ?? FileSystem.Data;

						// Default new file name
						string newFileName = $"New {friendlyName}.{ext}";
						string newFilePath = $"{entry.RealPath}/{newFileName}";

						// Ensure unique file name
						int count = 1;
						while ( fs.FileExists( newFilePath ) )
						{
							newFileName = $"New {friendlyName} ({count}).{ext}";
							newFilePath = $"{entry.RealPath}/{newFileName}";
							count++;
						}

						fs.WriteAllText( newFilePath, "" );
						Refresh();
						_currentContextMenu?.Delete();
					} );
				}
			}
		}
		);

		_currentContextMenu.AddSeparator();
		_currentContextMenu.AddMenuItem( "Properties", () => Log.Info( $"Properties for {CurrentPath}" ) );
	}

	// Optional: Show context menu for file/folder items
	private void ShowItemContextMenu( FileItem item, MousePanelEvent e )
	{
		if ( item == null ) return;

		_currentContextMenu?.Delete();
		_currentContextMenu = new ContextMenu( this, ContextMenu.PositionMode.UnderMouse );

		_currentContextMenu.AddMenuItem( "Open", () =>
		{
			if ( item.IsDirectory )
			{
				HandleDirectoryOpened( item.FullPath );
			}
			else
			{
				HandleFileOpened( item.FullPath );
			}
			_currentContextMenu?.Delete();
		} );

		_currentContextMenu.AddMenuItem( "Rename", () =>
		{
			// Find the ListViewItem for this FileItem
			var listViewItem = ListView.Items.FirstOrDefault( i => i.Data == item );
			if ( listViewItem != null )
			{
				listViewItem.BeginRename( newName =>
				{
					if ( !string.IsNullOrWhiteSpace( newName ) && newName != item.Name )
					{
						// Perform rename in the file system
						var entry = _virtualFileSystem.GetEntry( item.FullPath );
						var fs = entry.AssociatedFileSystem ?? FileSystem.Data;
						string newPath = System.IO.Path.Combine( System.IO.Path.GetDirectoryName( entry.RealPath ), newName );

						if ( item.IsDirectory )
						{
							if ( fs.DirectoryExists( entry.RealPath ) )
								MoveDirectory( fs, entry.RealPath, newPath );
						}
						else
						{
							if ( fs.FileExists( entry.RealPath ) )
								MoveFile( fs, entry.RealPath, newPath );
						}
						Refresh();
					}
				} );
			}
			_currentContextMenu?.Delete();
		} );

		_currentContextMenu.AddMenuItem( "Delete", () =>
		{
			if ( item.IsDirectory )
			{
				var entry = _virtualFileSystem.GetEntry( item.FullPath );
				var fs = entry.AssociatedFileSystem ?? FileSystem.Data;
				if ( fs.DirectoryExists( entry.RealPath ) )
				{
					fs.DeleteDirectory( entry.RealPath );
					Refresh();
				}
				else
				{
					Log.Warning( $"Directory not found for deletion: {item.FullPath}" );
				}
			}
			else
			{
				var entry = _virtualFileSystem.GetEntry( item.FullPath );
				var fs = entry.AssociatedFileSystem ?? FileSystem.Data;
				if ( fs.FileExists( entry.RealPath ) )
				{
					fs.DeleteFile( entry.RealPath );
					Refresh();
				}
				else
				{
					Log.Warning( $"File not found for deletion: {item.FullPath}" );
				}
			}
			_currentContextMenu?.Delete();
		} );

		_currentContextMenu.AddSeparator();
		_currentContextMenu.AddMenuItem( "Properties", () => Log.Info( $"Properties for {item.FullPath}" ) );
	}

	// Add these methods to VirtualFileBrowserView

	public void MoveFile( BaseFileSystem fs, string oldPath, string newPath )
	{
		if ( !fs.FileExists( oldPath ) )
		{
			Log.Warning( $"File not found for moving: {oldPath}" );
			return;
		}

		// Read file content
		var content = fs.ReadAllBytes( oldPath );
		// Write to new location
		var stream = fs.OpenWrite( newPath );
		stream.Write( content.ToArray(), 0, content.Length );
		// Delete original
		fs.DeleteFile( oldPath );
	}

	public void MoveDirectory( BaseFileSystem fs, string oldPath, string newPath )
	{
		if ( !fs.DirectoryExists( oldPath ) )
		{
			Log.Warning( $"Directory not found for moving: {oldPath}" );
			return;
		}

		// Recursively copy all files and subdirectories
		CopyDirectoryRecursive( fs, oldPath, newPath );

		// Delete the original directory
		fs.DeleteDirectory( oldPath );
	}

	private void CopyDirectoryRecursive( BaseFileSystem fs, string sourceDir, string destDir )
	{
		if ( !fs.DirectoryExists( destDir ) )
			fs.CreateDirectory( destDir );

		// Copy files
		foreach ( var file in fs.FindFile( sourceDir ) )
		{
			var fileName = System.IO.Path.GetFileName( file );
			var sourceFile = System.IO.Path.Combine( sourceDir, fileName );
			var destFile = System.IO.Path.Combine( destDir, fileName );
			var content = fs.ReadAllBytes( sourceFile );
			var stream = fs.OpenWrite( destFile );
			stream.Write( content.ToArray(), 0, content.Length );
		}

		// Copy subdirectories
		foreach ( var dir in fs.FindDirectory( sourceDir ) )
		{
			var dirName = System.IO.Path.GetFileName( dir );
			var sourceSubDir = System.IO.Path.Combine( sourceDir, dirName );
			var destSubDir = System.IO.Path.Combine( destDir, dirName );
			CopyDirectoryRecursive( fs, sourceSubDir, destSubDir );
		}
	}

	/// <summary>
	/// Override the refresh method to handle virtual paths
	/// </summary>
	public override void Refresh()
	{
		if ( _usingVirtualMode && !string.IsNullOrEmpty( _currentVirtualPath ) )
		{
			NavigateToVirtualPath( _currentVirtualPath, sound: false );
		}
		else
		{
			base.Refresh();
		}
	}

	private string GetCustomFolderIconFromDesktopIni( string virtualPath )
	{
		// Build the path to the desktop.ini file
		string iniVirtualPath = virtualPath + "/desktop.ini";
		var iniEntry = _virtualFileSystem.GetEntry( iniVirtualPath );
		if ( iniEntry == null )
			return null;

		var fs = iniEntry.AssociatedFileSystem ?? FileSystem.Data;
		if ( string.IsNullOrWhiteSpace( iniEntry.RealPath ) || !fs.FileExists( iniEntry?.RealPath ) )
			return null;

		// Read the file contents
		string[] lines = fs.ReadAllText( iniEntry.RealPath ).Split( '\n' );
		bool inSection = false;
		foreach ( var rawLine in lines )
		{
			string line = rawLine.Trim();
			if ( line.StartsWith( "[.XGUIInfo]", StringComparison.OrdinalIgnoreCase ) )
			{
				inSection = true;
				continue;
			}
			if ( inSection )
			{
				if ( line.StartsWith( "[" ) && line.EndsWith( "]" ) )
					break; // New section, stop
				if ( line.StartsWith( "Icon=", StringComparison.OrdinalIgnoreCase ) )
				{
					return line.Substring( "Icon=".Length ).Trim();
				}
			}
		}
		return null;
	}
}
