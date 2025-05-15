using System;
using System.IO;

namespace FakeOperatingSystem.Shell;

public class Shell
{
	public static void ShellExecute( string path )
	{
		// For executables, launch directly
		if ( path.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Info( $"Launching executable: {path}" );
			var launchOptions = new Win32LaunchOptions
			{
				WorkingDirectory = Path.GetDirectoryName( path )
			};
			ProcessManager.Instance?.OpenExecutable( path, launchOptions, shellLaunch: true );
			return;
		}

		// For other files, use file associations
		FileAssociationManager.Instance.OpenFile( path );
	}
}
