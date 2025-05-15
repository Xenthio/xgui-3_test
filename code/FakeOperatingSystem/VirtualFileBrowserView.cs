using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.Shell;
using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XGUI;

namespace FakeDesktop;

/// <summary>
/// Extension of FileBrowserView that works with both BaseFileSystem and VirtualFileSystem
/// </summary>
public class VirtualFileBrowserView : FileBrowserView
{
	// Replace the old monolithic VFS with our new components
	private IVirtualFileSystem _vfs;
	private ShellNamespace _shellManager;

	// Track if we're using virtual mode or regular mode
	private bool _usingVirtualMode = true;  // Default to shell namespace browsing

	// Current shell path (when in virtual mode)
	private string _currentShellPath = ShellNamespace.DESKTOP;

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
	public void Initialize( IVirtualFileSystem vfs, ShellNamespace shellManager, BaseFileSystem defaultFileSystem )
	{
		_vfs = vfs;
		_shellManager = shellManager;
		base.CurrentFileSystem = defaultFileSystem;

		// Navigate to Desktop (root of the shell namespace)
		NavigateToShellPath( ShellNamespace.DESKTOP, sound: false );
	}

	/// <summary>
	/// Navigate to a shell namespace path
	/// </summary>
	public void NavigateToShellPath( string shellPath, bool sound = true )
	{
		if ( _shellManager == null )
			return;

		// Save to history
		if ( _historyIndex >= 0 && _historyIndex < _navigationHistory.Count - 1 )
		{
			// If navigating from middle of history, truncate forward items
			_navigationHistory.RemoveRange( _historyIndex + 1, _navigationHistory.Count - _historyIndex - 1 );
		}
		_navigationHistory.Add( shellPath );
		_historyIndex = _navigationHistory.Count - 1;

		// Update current path tracking
		_currentShellPath = shellPath;
		_usingVirtualMode = true;

		// Get the shell folder
		var folder = _shellManager.GetFolder( shellPath );
		if ( folder == null )
		{
			Log.Warning( $"Shell path not found: {shellPath}" );
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
		if ( !string.IsNullOrEmpty( folder.RealPath ) )
		{
			// If folder has a real path, set the file system and path
			base.CurrentPath = folder.RealPath;
		}
		else
		{
			// For purely virtual folders, we'll use a placeholder path
			base.CurrentPath = "shell:" + shellPath;
		}

		// Trigger navigation event
		RaiseNavigateToEvent( _currentShellPath );

		// Add directory contents
		PopulateFromShellPath( shellPath );
	}

	/// <summary>
	/// Navigate back in history
	/// </summary>
	public void GoBack()
	{
		if ( _historyIndex > 0 )
		{
			_historyIndex--;
			NavigateToShellPath( _navigationHistory[_historyIndex] );
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
			NavigateToShellPath( _navigationHistory[_historyIndex] );
		}
	}

	/// <summary>
	/// Navigate to parent folder
	/// </summary>
	public void GoUp()
	{
		if ( string.IsNullOrEmpty( _currentShellPath ) || _currentShellPath == ShellNamespace.DESKTOP )
			return;

		int lastSlash = _currentShellPath.LastIndexOf( '/' );
		if ( lastSlash > 0 )
		{
			string parentPath = _currentShellPath.Substring( 0, lastSlash );
			NavigateToShellPath( parentPath );
		}
		else
		{
			// If no slashes, go to root Desktop
			NavigateToShellPath( ShellNamespace.DESKTOP );
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
					NavigateToShellPath( path );
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

		PlayDoubleClickSound();

		if ( _usingVirtualMode )
		{
			var shellItem = _shellManager.GetItems( _currentShellPath )
				.FirstOrDefault( i => i.Path == path );

			if ( shellItem != null )
			{
				// Handle control panel applets
				if ( shellItem.Type == ShellFolderType.ControlPanelApplet )
				{
					Log.Info( $"Opening Control Panel applet: {shellItem.Name}" );
					// Launch appropriate control panel window
					return;
				}

				// Shell execute for other files
				Shell.ShellExecute( shellItem.RealPath );
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
	private void PopulateFromShellPath( string shellPath )
	{
		// Get all items in the shell folder
		var items = _shellManager.GetItems( shellPath );

		// Process folders first
		foreach ( var item in items.Where( i => i.IsFolder ) )
		{
			AddDirectoryToView( item.Path, true, item.Name );
			UpdateItemIcon( item, true );
		}

		// Then process files
		foreach ( var item in items.Where( i => !i.IsFolder ) )
		{
			AddFileToView( item.Path, true, item.Name );
			UpdateItemIcon( item, false );
		}
	}


	/// <summary>
	/// Update the icon for a specific item
	/// </summary>
	private void UpdateItemIcon( ShellItem item, bool isDirectory )
	{
		var path = item.Path;
		var realPath = item.RealPath;
		var iconName = item.IconName;
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
					string customIcon = FileIconHelper.GetCustomFolderIconFromDesktopIni( realPath, _vfs );
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
			var associations = FileAssociationManager.Instance.GetAllAssociations();

			if ( associations != null )
			{
				submenu.AddMenuItem( "Folder", () =>
				{
					// Get the current shell folder to find its real path
					var currentFolder = _shellManager.GetFolder( _currentShellPath );
					if ( currentFolder?.RealPath != null )
					{
						string newFolderName = "New Folder";
						string newFolderPath = Path.Combine( currentFolder.RealPath, newFolderName );

						// Ensure unique folder name
						int count = 1;
						while ( _vfs.DirectoryExists( newFolderPath ) )
						{
							newFolderName = $"New Folder ({count})";
							newFolderPath = Path.Combine( currentFolder.RealPath, newFolderName );
							count++;
						}

						// Create the new folder
						_vfs.CreateDirectory( newFolderPath );
						Refresh();
					}
					_currentContextMenu?.Delete();
				} );

				submenu.AddSeparator();

				// Sort by friendly name for a nice menu
				foreach ( var assoc in associations.OrderBy( a => a.FriendlyName ) )
				{
					if ( !assoc.ShouldShowInShellCreateNew )
						continue;

					string ext = assoc.Extension.TrimStart( '.' );
					string friendlyName = assoc.FriendlyName ?? ext.ToUpper() + " File";
					string iconName = assoc.IconName ?? "";

					submenu.AddMenuItem( friendlyName, () =>
					{
						// Get the current shell folder to find its real path
						var currentFolder = _shellManager.GetFolder( _currentShellPath );
						if ( currentFolder?.RealPath != null )
						{
							// Default new file name
							string newFileName = $"New {friendlyName}.{ext}";
							string newFilePath = Path.Combine( currentFolder.RealPath, newFileName );

							// Ensure unique file name
							int count = 1;
							while ( _vfs.FileExists( newFilePath ) )
							{
								newFileName = $"New {friendlyName} ({count}).{ext}";
								newFilePath = Path.Combine( currentFolder.RealPath, newFileName );
								count++;
							}

							// Create the new file
							_vfs.WriteAllText( newFilePath, "" );
							Refresh();
						}
						_currentContextMenu?.Delete();
					} );
				}
			}
		} );

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
						// Get the shell item corresponding to this file/folder
						var shellItem = _shellManager.GetItems( _currentShellPath )
							.FirstOrDefault( i => i.Path == item.FullPath );

						if ( shellItem?.RealPath != null )
						{
							// Calculate the new path with the updated name
							string parentDir = Path.GetDirectoryName( shellItem.RealPath );
							string newPath = Path.Combine( parentDir, newName );

							// Perform the rename operation using the VFS
							if ( item.IsDirectory )
							{
								if ( _vfs.DirectoryExists( shellItem.RealPath ) )
								{
									// Use the existing move method
									MoveDirectory( FileSystem.Data, shellItem.RealPath, newPath );
								}
							}
							else
							{
								if ( _vfs.FileExists( shellItem.RealPath ) )
								{
									// Use the existing move method
									MoveFile( FileSystem.Data, shellItem.RealPath, newPath );
								}
							}

							Refresh();
						}
					}
				} );
			}
			_currentContextMenu?.Delete();
		} );

		_currentContextMenu.AddMenuItem( "Delete", () =>
		{
			if ( item.IsDirectory )
			{
				// Get the shell item's real path and use the VFS to delete it
				var shellItem = _shellManager.GetItems( _currentShellPath )
					.FirstOrDefault( i => i.Path == item.FullPath );

				if ( shellItem?.RealPath != null )
				{
					_vfs.DeleteDirectory( shellItem.RealPath );
					Refresh();
				}
			}
			else
			{
				var shellItem = _shellManager.GetItems( _currentShellPath )
					.FirstOrDefault( i => i.Path == item.FullPath );

				if ( shellItem?.RealPath != null )
				{
					_vfs.DeleteFile( shellItem.RealPath );
					Refresh();
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
		if ( _usingVirtualMode && !string.IsNullOrEmpty( _currentShellPath ) )
		{
			NavigateToShellPath( _currentShellPath, sound: false );
		}
		else
		{
			base.Refresh();
		}
	}
}
