using FakeOperatingSystem;
using FakeOperatingSystem.OSFileSystem;
using Sandbox;
using System;

/// <summary>
/// Mimics the behaviour of cmd.exe (MS-DOS Style Command Prompt) in Windows.
/// </summary>
public class CommandProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/System32/cmd.exe";
	public override bool ConsoleApp => true;

	public override void Main( NativeProcess process, Win32LaunchOptions launchOptions = null )
	{
		string workingDir = String.IsNullOrWhiteSpace( launchOptions?.WorkingDirectory ) ? @"C:\" : launchOptions.WorkingDirectory;
		StandardOutput.WriteLine( "" );
		StandardOutput.WriteLine( "" );
		StandardOutput.WriteLine( "XGUI-3 FakeOS" );
		StandardOutput.WriteLine( "   FakeOS Command Prompt [Type 'help' for commands]" );
		StandardOutput.WriteLine( "" );

		while ( true )
		{
			StandardOutput.Write( $"{workingDir}>" );
			var line = StandardInput.ReadLine();
			if ( line == null )
				break;

			StandardOutput.WriteLine( line );

			var commandLine = line.Trim();
			if ( string.IsNullOrEmpty( commandLine ) )
				continue;

			var parts = commandLine.Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries );
			var command = parts[0].ToLowerInvariant();
			var args = parts.Length > 1 ? parts[1] : "";

			switch ( command )
			{
				case "exit":
					StandardOutput.WriteLine( "Exiting..." );
					return;

				case "echo":
					StandardOutput.WriteLine( args );
					break;

				case "help":
					StandardOutput.WriteLine( "Built-in commands:" );
					StandardOutput.WriteLine( "  help         Show this help" );
					StandardOutput.WriteLine( "  echo <text>  Print text" );
					StandardOutput.WriteLine( "  cd <dir>     Change directory" );
					StandardOutput.WriteLine( "  dir          List files in current directory" );
					StandardOutput.WriteLine( "  exit         Exit the shell" );
					StandardOutput.WriteLine( "  <program>    Launch a program (e.g. notepad)" );
					break;

				case "cd":
					if ( string.IsNullOrWhiteSpace( args ) )
					{
						StandardOutput.WriteLine( workingDir );
					}
					else
					{
						string newDir = ResolvePath( workingDir, args );
						var entry = VirtualFileSystem.Instance.DirectoryExists( newDir );
						if ( VirtualFileSystem.Instance.DirectoryExists( newDir ) )
						{
							workingDir = newDir;
						}
						else
						{
							StandardOutput.WriteLine( $"The system cannot find the path specified: {args}" );
						}
					}
					break;

				case "dir":
					{
						if ( VirtualFileSystem.Instance.DirectoryExists( workingDir ) )
						{
							foreach ( var child in VirtualFileSystem.Instance.GetDirectories( workingDir ) )
							{
								var dirName = VirtualFileSystem.Instance.GetFileName( child );
								StandardOutput.WriteLine( $"<DIR> {dirName}" );
							}
							foreach ( var child in VirtualFileSystem.Instance.GetFiles( workingDir ) )
							{
								var fileName = VirtualFileSystem.Instance.GetFileName( child );
								StandardOutput.WriteLine( $"{VirtualFileSystem.Instance.FileSize( child ),8} {fileName}" );
							}
						}
						else
						{
							StandardOutput.WriteLine( "Directory not found." );
						}
					}
					break;

				default:
					// Try to launch as a program
					string exeName = command.EndsWith( ".exe" ) ? command : command + ".exe";
					string exePath = $"C:/Windows/System32/{exeName}";
					if ( VirtualFileSystem.Instance.FileExists( exePath ) )
					{
						try
						{
							var childOptions = new Win32LaunchOptions
							{
								Arguments = args,
								WorkingDirectory = workingDir,
								ParentProcessId = process.ProcessId,
								StandardOutputOverride = StandardOutput,
								StandardInputOverride = StandardInput
							};
							var newProcess = ProcessManager.Instance.OpenExecutable( exePath, childOptions );
							if ( newProcess.IsConsoleProcess )
							{
								// wait for the console process to finish
								while ( newProcess.Status == ProcessStatus.Running )
								{
									// cant use system.threading here
									//System.Threading.Thread.Sleep( 100 );
									GameTask.Delay( 100 ).Wait();
								}
							}
						}
						catch ( Exception ex )
						{
							StandardOutput.WriteLine( $"Failed to launch '{command}': {ex.Message}" );
						}
					}
					else
					{
						StandardOutput.WriteLine( $"'{command}' is not recognized as an internal or external command,\n operable program or batch file." );
					}
					break;
			}
		}
	}

	// Helper to resolve relative/absolute paths (no System.IO)
	private string ResolvePath( string currentDir, string input )
	{
		if ( input.StartsWith( "/" ) || input.StartsWith( "\\" ) || input.Contains( ":" ) )
			return input.Replace( '\\', '/' );
		if ( currentDir.EndsWith( "/" ) || currentDir.EndsWith( "\\" ) )
			return currentDir + input;
		return currentDir + "/" + input;
	}

	private string FormatDir( string dir )
	{
		return dir.Replace( '/', '\\' );
	}
}
