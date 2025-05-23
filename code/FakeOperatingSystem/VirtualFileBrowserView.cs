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

				string icon = "";
				if ( isDirectory )
				{
					icon = FileIconHelper.GetFolderIcon( realPath, size );
				}
				else
				{
					icon = FileIconHelper.GetFileIcon( realPath, size );
				}

				if ( icon == FileIconHelper.GetGenericFolderIcon( size ) )
				{
					iconPanel.SetIcon( iconName, XGUIIconSystem.IconType.Folder, size );
				}
				else
				{
					iconPanel.SetIcon( $"url:{icon}", iconSize: size );
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

		// "View" submenu
		_currentContextMenu.AddSubmenuItem( "View", submenu =>
		{
			submenu.AddMenuItem( "Icons", () => { ViewMode = FileBrowserViewMode.Icons; } );
			submenu.AddMenuItem( "List", () => { ViewMode = FileBrowserViewMode.List; } );
			submenu.AddMenuItem( "Details", () => { ViewMode = FileBrowserViewMode.Details; } );
		} );

		_currentContextMenu.AddSeparator();

		// Customise this folder
		_currentContextMenu.AddMenuItem( "Customize this Folder...", () =>
		{
			// Open the folder properties dialog
			var folder = _shellManager.GetFolder( _currentShellPath );
			if ( folder != null )
			{
				// Open the customise dialog for the folder
				// todo: Implement the customise dialog
			}
			_currentContextMenu?.Delete();
		} );

		_currentContextMenu.AddSeparator();

		// "Arrange Icons" submenu 
		_currentContextMenu.AddSubmenuItem( "Arrange Icons", submenu =>
		{
			submenu.AddMenuItem( "By Name", () => { /* Implement sorting by name */ } );
			submenu.AddMenuItem( "By Type", () => { /* Implement sorting by type */ } );
			submenu.AddMenuItem( "By Size", () => { /* Implement sorting by size */ } );
			submenu.AddMenuItem( "By Date", () => { /* Implement sorting by date modified */ } );
			submenu.AddSeparator();
			submenu.AddMenuItem( "Auto Arrange", () => { /* Implement auto arrange */ } );
		} );

		// "Line Up Icons" menu
		_currentContextMenu.AddMenuItem( "Line Up Icons", () => { } );

		_currentContextMenu.AddSeparator();

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
		_currentContextMenu.AddMenuItem( "Properties", () =>
		{
			var folder = _shellManager.GetFolder( _currentShellPath );
			if ( folder != null )
			{
				var dialog = new FilePropertiesDialog
				{
					Name = folder.Name,
					Path = folder.RealPath ?? _currentShellPath,
					IsDirectory = true,
					Size = 0 // You can calculate folder size if needed
				};
				XGUISystem.Instance.Panel.AddChild( dialog );
			}
			_currentContextMenu?.Delete();
		} );
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
									MoveDirectory( shellItem.RealPath, newPath );
								}
							}
							else
							{
								if ( _vfs.FileExists( shellItem.RealPath ) )
								{
									// Use the existing move method
									MoveFile( shellItem.RealPath, newPath );
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
		_currentContextMenu.AddMenuItem( "Properties", () =>
		{
			var shellItem = _shellManager.GetItems( _currentShellPath )
				.FirstOrDefault( i => i.Path == item.FullPath );

			if ( shellItem != null )
			{
				var isDir = item.IsDirectory;
				var size = isDir ? VirtualFileSystem.Instance.RecursiveDirectorySize( shellItem.RealPath ) : _vfs.FileSize( shellItem.RealPath );
				var dialog = new FilePropertiesDialog
				{
					Name = item.Name,
					Path = shellItem.RealPath ?? item.FullPath,
					IsDirectory = isDir,
					Size = size
				};
				XGUISystem.Instance.Panel.AddChild( dialog );


				// Get the size of the parent panel (this)
				var parentWidth = this.Box.Right - this.Box.Left;
				var parentHeight = this.Box.Bottom - this.Box.Top;

				// Get the size of the dialog 
				var dialogWidth = dialog.Box.Right - dialog.Box.Left;
				var dialogHeight = dialog.Box.Bottom - dialog.Box.Top;

				// Calculate the top-left position to center the dialog
				var x = (int)(parentWidth / 2 - dialogWidth / 2);
				var y = (int)(parentHeight / 2 - dialogHeight / 2);

				dialog.Position = new Vector2( x, y );

			}
			_currentContextMenu?.Delete();
		} );
	}

	// Add these methods to VirtualFileBrowserView

	public void MoveFile( string oldPath, string newPath )
	{
		if ( !_vfs.FileExists( oldPath ) )
		{
			Log.Warning( $"File not found for moving: {oldPath}" );
			return;
		}

		// Read file content
		var content = _vfs.ReadAllBytes( oldPath );
		// Write to new location
		var stream = _vfs.OpenWrite( newPath );
		stream.Write( content.ToArray(), 0, content.Length );
		// Delete original
		_vfs.DeleteFile( oldPath );
	}

	public void MoveDirectory( string oldPath, string newPath )
	{
		if ( !_vfs.DirectoryExists( oldPath ) )
		{
			Log.Warning( $"Directory not found for moving: {oldPath}" );
			return;
		}

		// Recursively copy all files and subdirectories
		CopyDirectoryRecursive( oldPath, newPath );

		// Delete the original directory
		_vfs.DeleteDirectory( oldPath );
	}

	private void CopyDirectoryRecursive( string sourceDir, string destDir )
	{
		if ( !_vfs.DirectoryExists( destDir ) )
			_vfs.CreateDirectory( destDir );

		// Copy files
		foreach ( var file in _vfs.FindFile( sourceDir ) )
		{
			var fileName = System.IO.Path.GetFileName( file );
			var sourceFile = System.IO.Path.Combine( sourceDir, fileName );
			var destFile = System.IO.Path.Combine( destDir, fileName );
			var content = _vfs.ReadAllBytes( sourceFile );
			_vfs.WriteAllBytes( destFile, content );
		}

		// Copy subdirectories
		foreach ( var dir in _vfs.FindDirectory( sourceDir ) )
		{
			var dirName = System.IO.Path.GetFileName( dir );
			var sourceSubDir = System.IO.Path.Combine( sourceDir, dirName );
			var destSubDir = System.IO.Path.Combine( destDir, dirName );
			CopyDirectoryRecursive( sourceSubDir, destSubDir );
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
