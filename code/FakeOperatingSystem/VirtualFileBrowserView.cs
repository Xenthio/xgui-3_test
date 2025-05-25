using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.Shell;
using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json; // Added for JSON serialization
using XGUI;

namespace FakeDesktop;

/// <summary>
/// Represents the data structure for storing icon position.
/// </summary>
public class IconPositionEntry
{
	public string FullPath { get; set; }
	public float X { get; set; }
	public float Y { get; set; }
}

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

	// Icon dragging state
	private const string ICON_POSITIONS_FILENAME = ".iconpositions.json";
	private ListView.ListViewItem _draggedListViewItem = null;
	private Vector2 _dragStartMousePos; // Mouse position when drag started, relative to the ListView
	private Vector2 _draggedItemInitialPos; // Initial position of the item being dragged

	public VirtualFileBrowserView() : base()
	{
		// Override the DirectoryOpened event to handle virtual navigation
		OnDirectoryOpened += HandleDirectoryOpened;
		OnFileOpened += HandleFileOpened;

		// Handle mouse move for dragging at the view level
		//StyleSheet.Parse( "VirtualFileBrowserView { position: absolute; }" ); // Ensure panel can capture mouse outside children
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );

		if ( _draggedListViewItem != null && ViewMode == FileBrowserViewMode.Icons )
		{
			// Calculate new position based on mouse movement
			// The position should be relative to the ListView panel itself.
			var listView = ListView; // Assuming ListView is the direct parent or container for items
			Vector2 localMousePos = e.LocalPosition;
			Vector2 newPos = _draggedItemInitialPos + (localMousePos - _dragStartMousePos);

			_draggedListViewItem.Style.Left = newPos.x;
			_draggedListViewItem.Style.Top = newPos.y;

			// Ensure the item's style reflects absolute positioning if not already handled by ListView in Icons mode
			_draggedListViewItem.Style.Position = PositionMode.Absolute;
			_draggedListViewItem.Style.Left = Length.Pixels( newPos.x );
			_draggedListViewItem.Style.Top = Length.Pixels( newPos.y );
		}
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

		// Save current icon positions if switching from Icons mode in a real folder
		var previousFolder = _shellManager.GetFolder( _currentShellPath );
		if ( ViewMode == FileBrowserViewMode.Icons && previousFolder?.RealPath != null )
		{
			SaveIconPositions( previousFolder.RealPath );
		}

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
		var shellFolder = _shellManager.GetFolder( shellPath );

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

		// Attach mouse handlers for dragging if in Icons view
		// And apply styles for absolute positioning if needed by the ListView's icon mode
		if ( ViewMode == FileBrowserViewMode.Icons )
		{
			// This ensures items can be freely positioned.
			// Depending on XGUI.ListView's implementation for Icons mode,
			// you might need to adjust its internal styling or panel type for items.
			// We assume setting Position on ListViewItem (which is a Panel) works.
			//ListView.Style.Position = PositionMode.Absolute; // Or ensure its item canvas is.
			foreach ( var listViewItem in ListView.Items )
			{
				listViewItem.Style.Position = PositionMode.Absolute; // Ensure each item can be positioned absolutely.


				//listViewItem.OnMouseDown += ( e ) => HandleItemMouseDown( listViewItem, e );
				listViewItem.AddEventListener( "onmousedown", ( PanelEvent e ) => HandleItemMouseDown( listViewItem, (e as MousePanelEvent) ) );
				//listViewItem.OnMouseUp += ( e ) => HandleItemMouseUp( listViewItem, e );
				listViewItem.AddEventListener( "onmouseup", ( PanelEvent e ) => HandleItemMouseUp( listViewItem, (e as MousePanelEvent) ) );
			}
		}

		// Load positions for icons if in icon view and folder has a real path
		if ( ViewMode == FileBrowserViewMode.Icons && shellFolder?.RealPath != null )
		{
			LoadIconPositions( shellFolder.RealPath );
		}
	}

	private void HandleItemMouseDown( ListView.ListViewItem item, MousePanelEvent e )
	{
		if ( e.Button == "mouseleft" && ViewMode == FileBrowserViewMode.Icons )
		{
			_draggedListViewItem = item;
			// Store the initial mouse position relative to the ListView panel
			_dragStartMousePos = e.LocalPosition;
			_draggedItemInitialPos = new Vector2( item.Style.Left.Value.Value, item.Style.Top.Value.Value );

			//item.CaptureMouse(); // Capture mouse to receive events even if cursor leaves item
			e.StopPropagation();
		}
	}

	private void HandleItemMouseUp( ListView.ListViewItem item, MousePanelEvent e )
	{
		if ( e.Button == "mouseleft" && _draggedListViewItem == item )
		{
			//_draggedListViewItem.ReleaseMouseCapture();
			_draggedListViewItem = null;

			var shellFolder = _shellManager.GetFolder( _currentShellPath );
			if ( shellFolder?.RealPath != null )
			{
				SaveIconPositions( shellFolder.RealPath );
			}
			e.StopPropagation();
		}
	}

	private void SaveIconPositions( string realFolderPath )
	{
		if ( string.IsNullOrEmpty( realFolderPath ) || ViewMode != FileBrowserViewMode.Icons )
			return;

		var positions = new List<IconPositionEntry>();
		foreach ( var listViewItem in ListView.Items )
		{
			if ( listViewItem.Data is FileItem fileItem )
			{
				// Use item.Position if it's directly settable and reflects the visual position.
				// Otherwise, you might need to use item.Style.Left and item.Style.Top.

				if ( listViewItem.Style.Left == null || listViewItem.Style.Top == null )
				{
					continue;
				}

				positions.Add( new IconPositionEntry
				{
					FullPath = fileItem.FullPath,
					X = listViewItem.Style.Left.Value.Value, // Assuming Position is updated correctly during drag
					Y = listViewItem.Style.Top.Value.Value
				} );
			}
		}

		string filePath = Path.Combine( realFolderPath, ICON_POSITIONS_FILENAME );
		try
		{
			string json = JsonSerializer.Serialize( positions, new JsonSerializerOptions { WriteIndented = true } );
			_vfs.WriteAllText( filePath, json );
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"Failed to save icon positions: {ex.Message}" );
		}
	}

	private void LoadIconPositions( string realFolderPath )
	{
		if ( string.IsNullOrEmpty( realFolderPath ) || ViewMode != FileBrowserViewMode.Icons || ListView == null )
			return;

		// Define grid parameters for default icon placement
		const float GRID_START_MARGIN_X = 5f;
		const float GRID_START_MARGIN_Y = 5f;
		const float DEFAULT_ICON_CELL_WIDTH = 80f;  // Includes icon width + padding for text + horizontal spacing
		const float DEFAULT_ICON_CELL_HEIGHT = 80f; // Includes icon height + padding for text + vertical spacing

		float listViewWidth = ListView.Box.Rect.Width;
		if ( listViewWidth <= GRID_START_MARGIN_X + DEFAULT_ICON_CELL_WIDTH ) // Check if enough space for at least one cell and margin
		{
			// Fallback: assume at least 5 cells wide if no layout info or too narrow
			listViewWidth = (DEFAULT_ICON_CELL_WIDTH * 5) + (GRID_START_MARGIN_X * 2);
		}

		float listViewHeight = ListView.Box.Rect.Height;
		if ( listViewHeight <= GRID_START_MARGIN_Y + DEFAULT_ICON_CELL_HEIGHT ) // Check if enough space for at least one cell and margin
		{
			// Fallback: assume at least 5 cells high if no layout info or too short
			listViewHeight = (DEFAULT_ICON_CELL_HEIGHT * 5) + (GRID_START_MARGIN_Y * 2);
		}

		string filePath = Path.Combine( realFolderPath, ICON_POSITIONS_FILENAME );
		Dictionary<string, Vector2> positionMap = null;

		if ( _vfs.FileExists( filePath ) )
		{
			try
			{
				string json = _vfs.ReadAllText( filePath );
				var positions = JsonSerializer.Deserialize<List<IconPositionEntry>>( json );
				if ( positions != null )
				{
					positionMap = positions.ToDictionary( p => p.FullPath, p => new Vector2( p.X, p.Y ) );
				}
			}
			catch ( System.Exception ex )
			{
				Log.Warning( $"Failed to load icon positions: {ex.Message}" );
			}
		}
		positionMap ??= new Dictionary<string, Vector2>(); // Ensure map is not null

		var flexDirection = ListView.ItemContainer.ComputedStyle?.FlexDirection ?? Sandbox.UI.FlexDirection.Row;

		float currentX = GRID_START_MARGIN_X;
		float currentY = GRID_START_MARGIN_Y;

		// Adjust starting position for reverse flows
		if ( flexDirection == Sandbox.UI.FlexDirection.RowReverse )
		{
			currentX = listViewWidth - DEFAULT_ICON_CELL_WIDTH - GRID_START_MARGIN_X;
			if ( currentX < GRID_START_MARGIN_X ) currentX = GRID_START_MARGIN_X; // Clamp if view too narrow
		}
		else if ( flexDirection == Sandbox.UI.FlexDirection.ColumnReverse )
		{
			currentY = listViewHeight - DEFAULT_ICON_CELL_HEIGHT - GRID_START_MARGIN_Y;
			if ( currentY < GRID_START_MARGIN_Y ) currentY = GRID_START_MARGIN_Y; // Clamp if view too short
		}

		foreach ( var listViewItem in ListView.Items )
		{
			listViewItem.Style.Position = PositionMode.Absolute; // Ensure absolute positioning

			if ( listViewItem.Data is FileItem fileItem )
			{
				if ( positionMap.TryGetValue( fileItem.FullPath, out var savedPos ) )
				{
					listViewItem.Style.Left = Length.Pixels( savedPos.x );
					listViewItem.Style.Top = Length.Pixels( savedPos.y );
				}
				else
				{
					// Apply default grid position based on flex direction
					switch ( flexDirection )
					{
						case Sandbox.UI.FlexDirection.Row:
							listViewItem.Style.Left = Length.Pixels( currentX );
							listViewItem.Style.Top = Length.Pixels( currentY );
							currentX += DEFAULT_ICON_CELL_WIDTH;
							if ( currentX + DEFAULT_ICON_CELL_WIDTH - GRID_START_MARGIN_X > listViewWidth )
							{
								currentX = GRID_START_MARGIN_X;
								currentY += DEFAULT_ICON_CELL_HEIGHT;
							}
							break;

						case Sandbox.UI.FlexDirection.Column:
							listViewItem.Style.Left = Length.Pixels( currentX );
							listViewItem.Style.Top = Length.Pixels( currentY );
							currentY += DEFAULT_ICON_CELL_HEIGHT;
							if ( currentY + DEFAULT_ICON_CELL_HEIGHT - GRID_START_MARGIN_Y > listViewHeight )
							{
								currentY = GRID_START_MARGIN_Y;
								currentX += DEFAULT_ICON_CELL_WIDTH;
							}
							break;

						case Sandbox.UI.FlexDirection.RowReverse:
							listViewItem.Style.Left = Length.Pixels( currentX );
							listViewItem.Style.Top = Length.Pixels( currentY );
							currentX -= DEFAULT_ICON_CELL_WIDTH;
							if ( currentX < GRID_START_MARGIN_X )
							{
								currentY += DEFAULT_ICON_CELL_HEIGHT;
								currentX = listViewWidth - DEFAULT_ICON_CELL_WIDTH - GRID_START_MARGIN_X;
								if ( currentX < GRID_START_MARGIN_X ) currentX = GRID_START_MARGIN_X;
							}
							break;

						case Sandbox.UI.FlexDirection.ColumnReverse:
							listViewItem.Style.Left = Length.Pixels( currentX );
							listViewItem.Style.Top = Length.Pixels( currentY );
							currentY -= DEFAULT_ICON_CELL_HEIGHT;
							if ( currentY < GRID_START_MARGIN_Y )
							{
								currentX += DEFAULT_ICON_CELL_WIDTH;
								currentY = listViewHeight - DEFAULT_ICON_CELL_HEIGHT - GRID_START_MARGIN_Y;
								if ( currentY < GRID_START_MARGIN_Y ) currentY = GRID_START_MARGIN_Y;
							}
							break;
					}
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
		var fileItem = FileItems.FirstOrDefault( fi => fi.FullPath == path );
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
		if ( _draggedListViewItem != null ) return; // Don't process click if it was the end of a drag
		_currentContextMenu?.Delete();
	}
	ContextMenu _currentContextMenu;
	protected override void OnRightClick( MousePanelEvent e )
	{
		base.OnRightClick( e );

		if ( _draggedListViewItem != null ) return; // Don't show context menu during/after a drag operation until next click

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
			submenu.AddMenuItem( "Icons", () => { ViewMode = FileBrowserViewMode.Icons; RefreshViewMode(); } );
			submenu.AddMenuItem( "List", () => { ViewMode = FileBrowserViewMode.List; RefreshViewMode(); } );
			submenu.AddMenuItem( "Details", () => { ViewMode = FileBrowserViewMode.Details; RefreshViewMode(); } );
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
			submenu.AddMenuItem( "Auto Arrange", () => { /* Implement auto arrange - this would disable free positioning and reset/remove .iconpositions.json */ } );
		} );

		// "Line Up Icons" menu
		_currentContextMenu.AddMenuItem( "Line Up Icons", () => { /* Implement snapping to a grid */ } );

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

	private void RefreshViewMode()
	{
		var shellFolder = _shellManager.GetFolder( _currentShellPath );
		// Detach old handlers if any, or rely on PopulateFromShellPath to recreate items
		// For simplicity, we'll let PopulateFromShellPath handle re-attaching

		// Update the ListView's mode
		base.UpdateViewMode(); // This calls ListView.UpdateViewMode

		// Re-apply icon specific logic if now in Icons mode
		if ( ViewMode == FileBrowserViewMode.Icons )
		{
			if ( shellFolder?.RealPath != null )
			{
				LoadIconPositions( shellFolder.RealPath );
			}
			// Re-attach handlers as items might be recreated or their properties reset by ListView.UpdateViewMode
			foreach ( var listViewItem in ListView.Items )
			{
				listViewItem.Style.Position = PositionMode.Absolute;
				// Clear old handlers before adding new ones to prevent duplicates if items are reused
				//listViewItem.RemoveEventListener("onmousedown");
				//listViewItem.RemoveEventListener("onmouseup");
				//listViewItem.OnMouseDown += (e) => HandleItemMouseDown(listViewItem, e);
				//listViewItem.OnMouseUp += (e) => HandleItemMouseUp(listViewItem, e);
			}
		}
		else
		{
			// If not in Icons mode, ensure items are not absolutely positioned by our logic
			// The ListView's own mode (List, Details) should take over.
			foreach ( var listViewItem in ListView.Items )
			{
				listViewItem.Style.Position = PositionMode.Relative;
				listViewItem.Style.Left = null;
				listViewItem.Style.Top = null;
				// Remove drag handlers
				//listViewItem.RemoveEventListener("onmousedown");
				//listViewItem.RemoveEventListener("onmouseup");
			}
			if ( shellFolder?.RealPath != null )
			{
				SaveIconPositions( shellFolder.RealPath ); // Save positions when leaving Icons mode
			}
		}
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
			// Save positions before refreshing, if applicable
			var shellFolder = _shellManager.GetFolder( _currentShellPath );
			if ( ViewMode == FileBrowserViewMode.Icons && shellFolder?.RealPath != null )
			{
				SaveIconPositions( shellFolder.RealPath );
			}
			NavigateToShellPath( _currentShellPath, sound: false );
		}
		else
		{
			base.Refresh();
		}
	}
}
