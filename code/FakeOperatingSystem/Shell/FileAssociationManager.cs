using FakeOperatingSystem.OSFileSystem;
using System;
using System.Collections.Generic;

namespace FakeOperatingSystem.Shell;

/// <summary>
/// Manages file type associations
/// </summary>
public class FileAssociationManager
{
	// Singleton instance
	public static FileAssociationManager Instance { get; private set; }

	// Dictionary of file associations by extension
	private Dictionary<string, FileAssociation> _associations = new( StringComparer.OrdinalIgnoreCase );

	// Reference to the VFS for file operations
	private IVirtualFileSystem _vfs;

	public FileAssociationManager( IVirtualFileSystem vfs )
	{
		Instance = this;
		_vfs = vfs;

		RegisterDefaultAssociations();
	}

	public static void Initialize( IVirtualFileSystem vfs )
	{
		new FileAssociationManager( vfs );
	}

	private void RegisterDefaultAssociations()
	{
		// Text files// Text files
		var txtAssociation = new FileAssociation( ".txt", "Text Document", "txt", "C:/Windows/notepad.exe", shouldShowInShellCreateNew: true );
		RegisterAssociation( txtAssociation );

		// HTML files
		var htmlAssociation = new FileAssociation( ".html", "HTML Document", "html", "C:/Program Files/Internet Explorer/iexplore.exe" );
		htmlAssociation.AddAction( "edit", "Edit", "notepad.exe" );
		RegisterAssociation( htmlAssociation );

		// WAD files (for Doom)
		var wadAssociation = new FileAssociation( ".wad", "Doom WAD File", "wad", "C:/Program Files/Ultimate Doom for Windows 95/doom95.exe" );
		RegisterAssociation( wadAssociation );

		// Shortcuts
		var lnkAssociation = new FileAssociation( ".lnk", "Shortcut", "lnk", null );
		// Shortcuts are handled specially in the file browser
		RegisterAssociation( lnkAssociation );

		// Ini files
		var iniAssociation = new FileAssociation( ".ini", "INI File", "ini", "C:/Windows/notepad.exe" );
		iniAssociation.AddAction( "edit", "Edit", "notepad.exe" );
		RegisterAssociation( iniAssociation );
	}

	public void RegisterAssociation( FileAssociation association )
	{
		string ext = association.Extension;
		if ( !ext.StartsWith( "." ) )
			ext = "." + ext;

		_associations[ext] = association;
	}

	public FileAssociation GetAssociation( string extension )
	{
		if ( string.IsNullOrEmpty( extension ) )
			return null;

		if ( !extension.StartsWith( "." ) )
			extension = "." + extension;

		if ( _associations.TryGetValue( extension, out var association ) )
			return association;

		return null;
	}

	public List<FileAssociation> GetAllAssociations()
	{
		return new List<FileAssociation>( _associations.Values );
	}

	public bool OpenFile( string filePath )
	{
		Log.Info( $"Opening file: {filePath}" );
		string extension = _vfs.GetExtension( filePath );
		var association = GetAssociation( extension );

		if ( association != null && !string.IsNullOrEmpty( association.DefaultProgram ) )
		{
			ProcessManager.Instance.OpenExecutable( association.DefaultProgram, new Win32LaunchOptions { Arguments = filePath } );
			return true;
		}

		return false;
	}
}
