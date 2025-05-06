using Sandbox;
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
		NavigateToVirtualPath( VirtualFileSystem.DESKTOP );
	}

	/// <summary>
	/// Navigate to a virtual path
	/// </summary>
	public void NavigateToVirtualPath( string virtualPath )
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

		// Clear existing items
		ItemContainer.DeleteChildren();
		FileItems.Clear();

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
			NavigateToVirtualPath( VirtualFileSystem.DESKTOP );
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

		// If not in virtual mode, or not a virtual path, use base navigation
		// This is already handled by the base class
	}

	/// <summary>
	/// Handle file opened event
	/// </summary>
	public virtual void HandleFileOpened( string path )
	{
		if ( !OpenFileEnabled )
			return;
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

				// EXE files are launched directly
				if ( entry.Name.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
				{
					var program = _virtualFileSystem.GetProgramFromFile( entry.RealPath );
					if ( program != null )
					{
						Log.Info( $"Launching program: {program.Name}" );
						program.Launch();
						return;
					}
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
		foreach ( var item in FileItems )
		{
			if ( item.FullPath == path )
			{
				if ( item.IconPanel is XGUIIconPanel iconPanel )
				{
					// Use proper icon type based on item type
					if ( isDirectory )
					{
						if ( string.IsNullOrEmpty( iconName ) || iconName == "folder" )
						{
							iconPanel.SetFolderIcon( "folder", 32 );
						}
						else
						{
							iconPanel.SetIcon( iconName, XGUIIconSystem.IconType.Folder, 32 );
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

						if ( string.IsNullOrEmpty( iconName ) || iconName == "file" )
						{
							iconPanel.SetFileIcon( extension, 32 );
						}
						else
						{
							iconPanel.SetIcon( iconName, XGUIIconSystem.IconType.FileType, 32 );
						}
					}
				}
				break;
			}
		}
	}

	/// <summary>
	/// Override the refresh method to handle virtual paths
	/// </summary>
	public override void Refresh()
	{
		if ( _usingVirtualMode && !string.IsNullOrEmpty( _currentVirtualPath ) )
		{
			NavigateToVirtualPath( _currentVirtualPath );
		}
		else
		{
			base.Refresh();
		}
	}
}
