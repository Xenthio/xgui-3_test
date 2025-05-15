using Sandbox;
using System.Collections.Generic;
using System.IO;

namespace FakeOperatingSystem.OSFileSystem
{
	/// <summary>
	/// Core virtual file system interface that handles actual file operations
	/// </summary>
	public interface IVirtualFileSystem
	{
		// Mount point management
		void RegisterMountPoint( string mountName, string path, BaseFileSystem fileSystem = null );
		bool IsMountPointValid( string mountPoint );

		// Basic file operations
		bool FileExists( string path );
		bool DirectoryExists( string path );
		Stream OpenRead( string path );
		Stream OpenWrite( string path );
		byte[] ReadAllBytes( string path );
		string ReadAllText( string path );
		void WriteAllBytes( string path, byte[] contents );
		void WriteAllText( string path, string contents );

		// Directory operations
		IEnumerable<string> GetFiles( string path, string searchPattern = "*" );
		IEnumerable<string> GetDirectories( string path );
		void CreateDirectory( string path );
		void DeleteFile( string path );
		void DeleteDirectory( string path, bool recursive = false );

		// Path operations
		string ResolvePath( string path );
		string GetFileName( string path );
		string GetDirectoryName( string path );
		string GetFileNameWithoutExtension( string path );
		string GetExtension( string path );

		// Misc
		long FileSize( string path );
		IEnumerable<string> FindFile( string sourceDir );
		IEnumerable<string> FindDirectory( string sourceDir );
	}
}
