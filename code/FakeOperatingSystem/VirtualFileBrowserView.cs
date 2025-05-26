using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.Shell;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using XGUI;
using static XGUI.ListView.ListViewItem;

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


	protected override bool WantsDragScrolling => false;

	// Store icon positions per folder (key: file path, value: position)
	private Dictionary<string, Dictionary<string, Vector2>> _iconPositions = new();

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

	public FlexDirection DefaultDirection = FlexDirection.Row;

	/// <summary>
	/// Populate the browser view from a virtual path
	/// </summary>
	private void PopulateFromShellPath( string shellPath )
	{
		LoadIconPositions( shellPath );

		var items = _shellManager.GetItems( shellPath );

		foreach ( var item in items.Where( i => i.IsFolder ) )
		{
			if ( ShouldFileBeVisible( item ) )
			{
				AddDirectoryToView( item.Path, true, item.Name );
				UpdateItemIcon( item, true );
			}
		}
		foreach ( var item in items.Where( i => !i.IsFolder ) )
		{
			if ( ShouldFileBeVisible( item ) )
			{
				AddFileToView( item.Path, true, item.Name );
				UpdateItemIcon( item, false );
			}
		}

		// Only apply icon positions and drag logic in icon view
		if ( ListView.ViewMode == ListView.ListViewMode.Icons && !AutoArrangeIcons )
		{
			var flexDirection = ListView.ItemContainer?.ComputedStyle?.FlexDirection ?? DefaultDirection;
			float iconWidth = 80f, iconHeight = 80f, spacingX = 2f, spacingY = 2f;
			float startX = 0f, startY = 0f;
			var availableWidth = ListView.Box.Rect.Width > 0 ? ListView.Box.Rect.Width : 800;
			int iconsPerRow = Math.Max( 1, (int)((availableWidth - startX) / (iconWidth + spacingX)) );

			int i = 0;
			if ( _iconPositions.TryGetValue( shellPath, out var positions ) == false )
				positions = new Dictionary<string, Vector2>();

			foreach ( var listViewItem in ListView.Items )
			{
				if ( listViewItem.Data is FileItem fileItem )
				{
					Vector2 pos;
					if ( !positions.TryGetValue( fileItem.FullPath, out pos ) )
					{
						if ( flexDirection == FlexDirection.Row )
						{
							int col = i % iconsPerRow;
							int row = i / iconsPerRow;
							pos = new Vector2( startX + col * (iconWidth + spacingX), startY + row * (iconHeight + spacingY) );
						}
						else
						{
							int row = i % iconsPerRow;
							int col = i / iconsPerRow;
							pos = new Vector2( startX + col * (iconWidth + spacingX), startY + row * (iconHeight + spacingY) );
						}
						positions[fileItem.FullPath] = pos;
					}
					listViewItem.Style.Position = PositionMode.Absolute;
					listViewItem.Style.Left = pos.x;
					listViewItem.Style.Top = pos.y;
					i++;

					MakeItemGhostDraggable( listViewItem );
				}
			}
			_iconPositions[shellPath] = positions;
		}
		else
		{
			// Reset any icon-specific styles in other views
			foreach ( var listViewItem in ListView.Items )
			{
				listViewItem.Style.Position = PositionMode.Relative;
				listViewItem.Style.Left = 0;
				listViewItem.Style.Top = 0;
				if ( listViewItem.Data is FileItem fileItem )
				{
					MakeItemGhostDraggable( listViewItem );
				}
			}
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
			submenu.AddMenuItem( "By Name", () => { ArrangeIcons(); } );
			submenu.AddMenuItem( "By Type", () => { /* Implement sorting by type if needed */ } );
			submenu.AddMenuItem( "By Size", () => { /* Implement sorting by size if needed */ } );
			submenu.AddMenuItem( "By Date", () => { /* Implement sorting by date if needed */ } );
			submenu.AddSeparator();
			submenu.AddMenuItem( "Auto Arrange", () =>
			{
				AutoArrangeIcons = !AutoArrangeIcons;
				ArrangeIcons();
				Refresh();
			} );
		} );

		// "Line Up Icons" menu
		_currentContextMenu.AddMenuItem( "Line Up Icons", () => { LineUpIconsToGrid(); } );

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

	public string IconPositionsFileName = ".iconpositions"; // Default positions file name

	// Path to the hidden positions file for the current folder
	private string GetPositionsFilePath( string folderPath )
	{
		var folder = _shellManager.GetFolder( folderPath );
		var realPath = folder?.RealPath;
		if ( string.IsNullOrEmpty( realPath ) ) return null;
		return Path.Combine( realPath, IconPositionsFileName );
	}

	// Load icon positions for the current folder
	private void LoadIconPositions( string folderPath )
	{
		var file = GetPositionsFilePath( folderPath );
		if ( file == null || !_vfs.FileExists( file ) )
		{
			_iconPositions[folderPath] = new();
			return;
		}
		try
		{
			var json = _vfs.ReadAllText( file );
			_iconPositions[folderPath] = JsonSerializer.Deserialize<Dictionary<string, Vector2>>( json ) ?? new();
		}
		catch
		{
			_iconPositions[folderPath] = new();
		}
	}

	// Save icon positions for the current folder
	private void SaveIconPositions( string folderPath )
	{
		var file = GetPositionsFilePath( folderPath );
		if ( file == null ) return;
		var json = JsonSerializer.Serialize( _iconPositions[folderPath] );
		_vfs.WriteAllText( file, json );
	}

	/// <summary>
	/// Make an item ghost draggable
	/// </summary>
	private void MakeItemGhostDraggable( ListView.ListViewItem item )
	{
		Panel ghost = null;
		Vector2 grabOffset = default;
		item.Draggable = true; // Enable dragging on the item

		// Start drag: create ghost and calculate offset
		item.OnDragStartEvent += ( ItemDragEvent e ) =>
		{
			grabOffset = e.LocalGrabPosition;

			ghost = new Panel();
			ghost.Parent = this;
			ghost.Style.Position = PositionMode.Absolute;


			var parentRect = item.Parent?.Box.Rect ?? Box.Rect;
			float parentLeft = parentRect.Left;
			float parentTop = parentRect.Top;
			float newLeft = e.ScreenPosition.x - parentLeft - grabOffset.x;
			float newTop = e.ScreenPosition.y - parentTop - grabOffset.y;
			ghost.Style.Left = newLeft;
			ghost.Style.Top = newTop;

			ghost.Style.Width = item.Box.Rect.Width;
			ghost.Style.Height = item.Box.Rect.Height;
			ghost.Style.Opacity = 0.79f;
			ghost.Style.ZIndex = 1000;

			//test background
			//ghost.Style.BackgroundColor = Color.Blue;

			ghost.Style.PointerEvents = PointerEvents.None;

			// copy classes from the original item
			ghost.Classes = item.Classes;
			ghost.AddClass( "ghost-drag" );
			ghost.RemoveClass( "selected" );

			if ( ListView.ViewMode == XGUI.ListView.ListViewMode.Icons )
			{
				// clone icon and label panels
				foreach ( var panel in item.Children )
				{
					if ( panel is XGUIIconPanel iconPanel )
					{
						var iconClone = new XGUIIconPanel();
						iconClone.SetIcon( iconPanel.IconName, iconPanel.IconType, iconPanel.IconSize );
						iconClone.Classes = iconPanel.Classes;
						ghost.AddChild( iconClone );
						continue;
					}
					else if ( panel is Label label )
					{
						var labelClone = new Label();
						labelClone.Text = label.Text;
						labelClone.Classes = label.Classes;
						ghost.AddChild( labelClone );
						continue;
					}
				}
			}
			else
			{
				// clone icon and label panels
				foreach ( var panel in item.Children.First().Children )
				{
					if ( panel is XGUIIconPanel iconPanel )
					{
						var iconClone = new XGUIIconPanel();
						iconClone.SetIcon( iconPanel.IconName, iconPanel.IconType, iconPanel.IconSize );
						iconClone.Classes = iconPanel.Classes;
						ghost.AddChild( iconClone );
						continue;
					}
					else if ( panel is Label label )
					{
						var labelClone = new Label();
						labelClone.Text = label.Text;
						labelClone.Classes = label.Classes;
						ghost.AddChild( labelClone );
						continue;
					}
				}
			}



			item.Parent?.AddChild( ghost ); // Ensure ghost is added to the correct parent panel

			item.Style.Cursor = "grabbing";
		};

		// During drag: move ghost
		item.OnDragEvent += ( ItemDragEvent e ) =>
		{
			if ( ghost == null ) return;

			// Calculate ghost position relative to the parent panel
			var parentRect = item.Parent?.Box.Rect ?? Box.Rect;
			float parentLeft = parentRect.Left;
			float parentTop = parentRect.Top;
			float newLeft = e.ScreenPosition.x - parentLeft - grabOffset.x;
			float newTop = e.ScreenPosition.y - parentTop - grabOffset.y;
			ghost.Style.Left = newLeft;
			ghost.Style.Top = newTop;
		};

		// End drag: move real item, save, and remove ghost
		item.OnDragEndEvent += ( ItemDragEvent e ) =>
		{
			if ( ghost != null )
			{
				if ( !AutoArrangeIcons )
				{
					item.Style.Left = ghost.Style.Left;
					item.Style.Top = ghost.Style.Top;

					if ( item.Data is FileBrowserView.FileItem fileItem )
					{
						if ( !_iconPositions.ContainsKey( _currentShellPath ) )
							_iconPositions[_currentShellPath] = new();
						_iconPositions[_currentShellPath][fileItem.FullPath] = new Vector2( item.Style.Left?.Value ?? 0, item.Style.Top?.Value ?? 0 );
						SaveIconPositions( _currentShellPath );
					}
				}

				ghost.Delete();
				ghost = null;
			}
		};
	}
	public bool AutoArrangeIcons = true;
	private void ArrangeIcons()
	{
		if ( ListView.ViewMode != XGUI.ListView.ListViewMode.Icons )
			return;

		var shellPath = _currentShellPath;
		var items = ListView.Items.Where( i => i.Data is FileItem ).ToList();

		// Sort: folders first (by name), then files (by name)
		items.Sort( ( a, b ) =>
		{
			var fa = a.Data as FileItem;
			var fb = b.Data as FileItem;
			if ( fa.IsDirectory && !fb.IsDirectory ) return -1;
			if ( !fa.IsDirectory && fb.IsDirectory ) return 1;
			return string.Compare( fa.Name, fb.Name, StringComparison.OrdinalIgnoreCase );
		} );

		// Get flex direction
		var flexDirection = ListView.ItemContainer?.ComputedStyle?.FlexDirection ?? DefaultDirection;

		// Arrange in grid
		float iconWidth = 80f, iconHeight = 80f, spacingX = 2f, spacingY = 2f;
		float startX = 0f, startY = 0f;
		var availableWidth = ListView.Box.Rect.Width > 0 ? ListView.Box.Rect.Width : 800;
		var availableHeight = ListView.Box.Rect.Height > 0 ? ListView.Box.Rect.Height : 600;

		int iconsPerRow = Math.Max( 1, (int)((availableWidth - startX) / (iconWidth + spacingX)) );
		int iconsPerCol = Math.Max( 1, (int)((availableHeight - startY) / (iconHeight + spacingY)) );

		var positions = new Dictionary<string, Vector2>();
		for ( int i = 0; i < items.Count; i++ )
		{
			var fileItem = items[i].Data as FileItem;
			int col, row;
			Vector2 pos;

			if ( flexDirection == FlexDirection.Row )
			{
				col = i % iconsPerRow;
				row = i / iconsPerRow;
				pos = new Vector2( startX + col * (iconWidth + spacingX), startY + row * (iconHeight + spacingY) );
			}
			else // FlexDirection.Column
			{
				row = i % iconsPerCol;
				col = i / iconsPerCol;
				pos = new Vector2( startX + col * (iconWidth + spacingX), startY + row * (iconHeight + spacingY) );
			}

			items[i].Style.Position = PositionMode.Absolute;
			items[i].Style.Left = pos.x;
			items[i].Style.Top = pos.y;
			positions[fileItem.FullPath] = pos;
		}
		_iconPositions[shellPath] = positions;
		SaveIconPositions( shellPath );
	}

	private void LineUpIconsToGrid()
	{
		if ( ListView.ViewMode != XGUI.ListView.ListViewMode.Icons )
			return;

		var shellPath = _currentShellPath;
		float iconWidth = 80f, iconHeight = 80f, spacingX = 2f, spacingY = 2f;
		float gridX = iconWidth + spacingX;
		float gridY = iconHeight + spacingY;

		if ( !_iconPositions.TryGetValue( shellPath, out var positions ) )
			return;

		foreach ( var item in ListView.Items )
		{
			if ( item.Data is FileItem fileItem && positions.TryGetValue( fileItem.FullPath, out var pos ) )
			{
				// Snap to nearest grid
				float snappedX = (float)Math.Round( pos.x / gridX ) * gridX;
				float snappedY = (float)Math.Round( pos.y / gridY ) * gridY;
				item.Style.Left = snappedX;
				item.Style.Top = snappedY;
				positions[fileItem.FullPath] = new Vector2( snappedX, snappedY );
			}
		}
		SaveIconPositions( shellPath );
	}
	private bool ShouldFileBeVisible( ShellItem item )
	{
		bool isHidden = false;

		// Hide dot files by default
		if ( item.Name.StartsWith( "." ) ) isHidden = true;

		// Hide system files (e.g. desktop.ini, thumbs.db, etc.)
		if ( item.Name.Equals( "desktop.ini", StringComparison.OrdinalIgnoreCase ) ||
			 item.Name.Equals( "thumbs.db", StringComparison.OrdinalIgnoreCase ) ||
			 item.Name.Equals( "iconcache.db", StringComparison.OrdinalIgnoreCase ) )
		{
			isHidden = true;
		}
		return !isHidden;
	}
}
