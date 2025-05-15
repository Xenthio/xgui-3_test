using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FakeOperatingSystem.OSFileSystem;

/// <summary>
/// Implementation of the virtual file system that focuses purely on file operations
/// </summary>
public class VirtualFileSystem : IVirtualFileSystem
{
	// Singleton instance
	public static VirtualFileSystem Instance { get; private set; }

	// Mount points map virtual drive letters to real paths
	private Dictionary<string, MountPoint> _mountPoints = new( StringComparer.OrdinalIgnoreCase );

	// Default file system for operations not associated with a specific mount
	private BaseFileSystem _defaultFileSystem;

	public VirtualFileSystem( BaseFileSystem defaultFileSystem )
	{
		Instance = this;
		_defaultFileSystem = defaultFileSystem;

		// Set up default mount points
		RegisterMountPoint( "C:", "FakeSystemRoot" );
		RegisterMountPoint( "FS:A:", "/", FileSystem.Mounted );
		RegisterMountPoint( "FS:B:", "/", FileSystem.OrganizationData );
		RegisterMountPoint( "FS:C:", "/", FileSystem.Data );
	}

	public void RegisterMountPoint( string mountName, string path, BaseFileSystem fileSystem = null )
	{
		_mountPoints[mountName] = new MountPoint
		{
			Name = mountName,
			RealPath = path,
			FileSystem = fileSystem ?? _defaultFileSystem
		};
	}

	public bool IsMountPointValid( string mountPoint )
	{
		return _mountPoints.ContainsKey( mountPoint );
	}

	/// <summary>
	/// Resolves a path with mount points to a real path and file system
	/// </summary>
	public PathResolution ResolveMountPoint( string path )
	{
		// Normalize the path
		path = path.Replace( '\\', '/' );

		// Handle shell namespace paths first
		if ( path.Contains( "/Desktop/" ) || path.Contains( "/My Computer/" ) )
		{
			// Extract the mount point part (e.g., "C:")
			if ( path.Contains( "/My Computer/" ) )
			{
				int mountStartIndex = path.IndexOf( "/My Computer/" ) + "/My Computer/".Length;
				int nextSlashIndex = path.IndexOf( '/', mountStartIndex );

				string mountPoint;
				string remainingPath = "";

				if ( nextSlashIndex > 0 )
				{
					mountPoint = path.Substring( mountStartIndex, nextSlashIndex - mountStartIndex );
					remainingPath = path.Substring( nextSlashIndex );
				}
				else
				{
					mountPoint = path.Substring( mountStartIndex );
				}

				// Check if this is a valid mount point
				if ( _mountPoints.TryGetValue( mountPoint, out var mount ) )
				{
					// Construct the proper path with the mount point
					string fullPath = Path.Combine( mount.RealPath, remainingPath.TrimStart( '/' ) ).Replace( '\\', '/' );

					return new PathResolution
					{
						MountPoint = mount,
						RealPath = fullPath,
						FileSystem = mount.FileSystem ?? _defaultFileSystem
					};
				}
			}
		}

		// Find the mount point
		foreach ( var mount in _mountPoints.Keys.OrderByDescending( k => k.Length ) )
		{
			if ( path.StartsWith( mount, StringComparison.OrdinalIgnoreCase ) )
			{
				var mountPoint = _mountPoints[mount];
				string relativePath = path.Substring( mount.Length ).TrimStart( '/' );
				string fullPath = Path.Combine( mountPoint.RealPath, relativePath ).Replace( '\\', '/' );

				return new PathResolution
				{
					MountPoint = mountPoint,
					RealPath = fullPath,
					FileSystem = mountPoint.FileSystem ?? _defaultFileSystem
				};
			}
		}

		// No mount point found - treat as a path on the default file system
		return new PathResolution
		{
			MountPoint = null,
			RealPath = path,
			FileSystem = _defaultFileSystem
		};
	}

	public string ResolvePath( string path )
	{
		var resolution = ResolveMountPoint( path );
		return resolution.RealPath;
	}

	public bool FileExists( string path )
	{
		var resolution = ResolveMountPoint( path );
		return resolution.FileSystem.FileExists( resolution.RealPath );
	}

	public bool DirectoryExists( string path )
	{
		var resolution = ResolveMountPoint( path );
		return resolution.FileSystem.DirectoryExists( resolution.RealPath );
	}

	public Stream OpenRead( string path )
	{
		var resolution = ResolveMountPoint( path );
		return resolution.FileSystem.OpenRead( resolution.RealPath );
	}

	public Stream OpenWrite( string path )
	{
		var resolution = ResolveMountPoint( path );
		return resolution.FileSystem.OpenWrite( resolution.RealPath );
	}

	public byte[] ReadAllBytes( string path )
	{
		var resolution = ResolveMountPoint( path );
		return resolution.FileSystem.ReadAllBytes( resolution.RealPath ).ToArray();
	}

	public string ReadAllText( string path )
	{
		var resolution = ResolveMountPoint( path );
		return resolution.FileSystem.ReadAllText( resolution.RealPath );
	}

	public void WriteAllBytes( string path, byte[] contents )
	{
		var resolution = ResolveMountPoint( path );

		resolution.FileSystem.OpenWrite( path ).Write( contents, 0, contents.Length );

		//resolution.FileSystem.WriteAllBytes( resolution.RealPath, contents );
	}

	public void WriteAllText( string path, string contents )
	{
		var resolution = ResolveMountPoint( path );
		resolution.FileSystem.WriteAllText( resolution.RealPath, contents );
	}

	public IEnumerable<string> GetFiles( string path, string searchPattern = "*" )
	{
		var resolution = ResolveMountPoint( path );
		var files = resolution.FileSystem.FindFile( resolution.RealPath );

		// Convert back to virtual paths if needed
		if ( resolution.MountPoint != null )
		{
			string mountPrefix = resolution.MountPoint.Name;
			return files.Select( f =>
			{
				// Normalize paths for comparison
				string normalizedRealPath = resolution.RealPath.TrimEnd( '/' );
				string normalizedFile = f.TrimEnd( '/' );

				// Calculate relative path safely
				if ( normalizedFile.StartsWith( normalizedRealPath, StringComparison.OrdinalIgnoreCase ) )
				{
					string relativePath = "";
					if ( normalizedFile.Length > normalizedRealPath.Length )
					{
						int startIndex = normalizedRealPath.Length;
						if ( normalizedFile.Length > startIndex && normalizedFile[startIndex] == '/' )
							startIndex++;

						relativePath = normalizedFile.Substring( startIndex );
					}
					return $"{mountPrefix}/{relativePath.TrimStart( '/' )}";
				}
				else
				{
					// If not in the path, return just the filename
					return $"{mountPrefix}/{Path.GetFileName( f )}";
				}
			} );
		}

		return files;
	}

	public IEnumerable<string> GetDirectories( string path )
	{
		var resolution = ResolveMountPoint( path );
		var dirs = resolution.FileSystem.FindDirectory( resolution.RealPath );

		// Convert back to virtual paths if needed
		if ( resolution.MountPoint != null )
		{
			string mountPrefix = resolution.MountPoint.Name;
			return dirs.Select( d =>
			{
				// Ensure both paths end with the same delimiter for correct comparison
				string normalizedRealPath = resolution.RealPath.TrimEnd( '/' );
				string normalizedDir = d.TrimEnd( '/' );

				// Make sure we only calculate the relative path if the directory is actually a subdirectory
				if ( normalizedDir.StartsWith( normalizedRealPath, StringComparison.OrdinalIgnoreCase ) )
				{
					// Calculate the relative path safely
					string relativePath = "";
					if ( normalizedDir.Length > normalizedRealPath.Length )
					{
						// +1 to skip the path separator if present
						int startIndex = normalizedRealPath.Length;
						if ( normalizedDir.Length > startIndex && normalizedDir[startIndex] == '/' )
							startIndex++;

						relativePath = normalizedDir.Substring( startIndex );
					}
					return $"{mountPrefix}/{relativePath.TrimStart( '/' )}";
				}
				else
				{
					// If not a subdirectory, return just the filename
					return $"{mountPrefix}/{Path.GetFileName( d )}";
				}
			} );
		}

		return dirs;
	}

	public void CreateDirectory( string path )
	{
		var resolution = ResolveMountPoint( path );
		resolution.FileSystem.CreateDirectory( resolution.RealPath );
	}

	public void DeleteFile( string path )
	{
		var resolution = ResolveMountPoint( path );
		resolution.FileSystem.DeleteFile( resolution.RealPath );
	}

	public void DeleteDirectory( string path, bool recursive = false )
	{
		var resolution = ResolveMountPoint( path );

		if ( recursive )
		{
			// Delete all files and subdirectories recursively
			foreach ( var file in GetFiles( path ) )
			{
				DeleteFile( file );
			}

			foreach ( var dir in GetDirectories( path ) )
			{
				DeleteDirectory( dir, true );
			}
		}

		resolution.FileSystem.DeleteDirectory( resolution.RealPath );
	}

	public string GetFileName( string path )
	{
		return Path.GetFileName( path );
	}

	public string GetDirectoryName( string path )
	{
		return Path.GetDirectoryName( path )?.Replace( '\\', '/' );
	}

	public string GetFileNameWithoutExtension( string path )
	{
		return Path.GetFileNameWithoutExtension( path );
	}

	public string GetExtension( string path )
	{
		return Path.GetExtension( path );
	}
}

/// <summary>
/// Represents a mount point in the virtual file system
/// </summary>
public class MountPoint
{
	/// <summary>
	/// The name of the mount point (e.g., "C:", "FS:A:")
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// The real path this mount point maps to
	/// </summary>
	public string RealPath { get; set; }

	/// <summary>
	/// The file system to use for this mount point
	/// </summary>
	public BaseFileSystem FileSystem { get; set; }
}

/// <summary>
/// Contains the result of resolving a virtual path to a real path
/// </summary>
public class PathResolution
{
	/// <summary>
	/// The mount point for the path, if any
	/// </summary>
	public MountPoint MountPoint { get; set; }

	/// <summary>
	/// The real path after resolution
	/// </summary>
	public string RealPath { get; set; }

	/// <summary>
	/// The file system to use for operations
	/// </summary>
	public BaseFileSystem FileSystem { get; set; }
}
