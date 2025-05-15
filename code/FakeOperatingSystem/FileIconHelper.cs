using Sandbox;
using System;
using System.IO;
using XGUI;

namespace FakeDesktop
{
	/// <summary>
	/// Helper class for extracting and working with file icons
	/// </summary>
	public static class FileIconHelper
	{
		/// <summary>
		/// Gets the appropriate icon for a file based on its extension or type
		/// </summary>
		public static string GetFileIcon( string path, OldVirtualFileSystem virtualFileSystem, int size = 16 )
		{
			if ( string.IsNullOrEmpty( path ) )
				return GetGenericFileIcon( size );

			var entry = virtualFileSystem.GetEntry( path );
			if ( entry == null )
				return GetGenericFileIcon( size );

			// Handle shortcuts - get icon from target
			if ( entry.Name.EndsWith( ".lnk", StringComparison.OrdinalIgnoreCase ) )
			{
				var shortcut = virtualFileSystem.GetShortcutFromFile( entry.RealPath );

				// Try to get icon from target if it's an executable
				if ( shortcut != null && !string.IsNullOrEmpty( shortcut.TargetPath ) )
				{
					if ( shortcut.TargetPath.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
					{
						string filename = Path.GetFileNameWithoutExtension( shortcut.TargetPath );
						return XGUIIconSystem.GetIcon( $"exe_{filename}", XGUIIconSystem.IconType.FileType, size );
					}
					return XGUIIconSystem.GetIcon( shortcut.IconName, XGUIIconSystem.IconType.FileType, size );
				}
			}

			// For executables, try to get icon from the filename
			if ( entry.Name.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
			{
				string filename = Path.GetFileNameWithoutExtension( entry.Name );
				return XGUIIconSystem.GetIcon( $"exe_{filename}", XGUIIconSystem.IconType.FileType, size );
			}

			// For files with custom icons defined
			if ( !string.IsNullOrEmpty( entry.IconName ) )
			{
				return XGUIIconSystem.GetIcon( entry.IconName, XGUIIconSystem.IconType.FileType, size );
			}

			// Fall back to extension-based icon lookup
			string extension = Path.GetExtension( entry.Name );
			if ( !string.IsNullOrEmpty( extension ) && extension.StartsWith( "." ) )
			{
				extension = extension.Substring( 1 );
				return XGUIIconSystem.GetIcon( extension, XGUIIconSystem.IconType.FileType, size );
			}

			return GetGenericFileIcon( size );
		}

		/// <summary>
		/// Gets the appropriate icon for a folder
		/// </summary>
		public static string GetFolderIcon( string path, OldVirtualFileSystem virtualFileSystem, int size = 16 )
		{
			if ( string.IsNullOrEmpty( path ) )
				return GetGenericFolderIcon( size );

			var entry = virtualFileSystem.GetEntry( path );
			if ( entry == null )
				return GetGenericFolderIcon( size );

			// Check for custom folder icon from desktop.ini
			string customIcon = GetCustomFolderIconFromDesktopIni( path, virtualFileSystem );
			if ( !string.IsNullOrEmpty( customIcon ) )
			{
				return XGUIIconSystem.GetIcon( customIcon, XGUIIconSystem.IconType.Folder, size );
			}

			// Use entry's defined icon if available
			if ( !string.IsNullOrEmpty( entry.IconName ) && entry.IconName != "folder" )
			{
				return XGUIIconSystem.GetIcon( entry.IconName, XGUIIconSystem.IconType.Folder, size );
			}

			return GetGenericFolderIcon( size );
		}

		/// <summary>
		/// Gets the default generic file icon
		/// </summary>
		public static string GetGenericFileIcon( int size = 16 )
		{
			return XGUIIconSystem.GetIcon( "file", XGUIIconSystem.IconType.FileType, size );
		}

		/// <summary>
		/// Gets the default generic folder icon
		/// </summary>
		public static string GetGenericFolderIcon( int size = 16 )
		{
			return XGUIIconSystem.GetIcon( "folder", XGUIIconSystem.IconType.Folder, size );
		}

		/// <summary>
		/// Gets a custom folder icon from a desktop.ini file if present
		/// </summary>
		public static string GetCustomFolderIconFromDesktopIni( string virtualPath, OldVirtualFileSystem virtualFileSystem )
		{
			// Build the path to the desktop.ini file
			string iniVirtualPath = virtualPath + "/desktop.ini";
			var iniEntry = virtualFileSystem.GetEntry( iniVirtualPath );
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
}
