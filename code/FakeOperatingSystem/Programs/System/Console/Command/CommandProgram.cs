using FakeDesktop;
using FakeOperatingSystem;
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
		string workingDir = launchOptions?.WorkingDirectory ?? "C:/";
		StandardOutput.WriteLine( "FakeOS Command Prompt [Type 'help' for commands]" );

		while ( true )
		{
			StandardOutput.Write( $"{workingDir}> " );
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
						var entry = OldVirtualFileSystem.Instance.GetEntry( newDir );
						if ( entry != null && entry.Type == OldVirtualFileSystem.EntryType.Directory )
						{
							workingDir = entry.Path;
						}
						else
						{
							StandardOutput.WriteLine( $"The system cannot find the path specified: {args}" );
						}
					}
					break;

				case "dir":
					{
						var entry = OldVirtualFileSystem.Instance.GetEntry( workingDir );
						var realFS = entry?.AssociatedFileSystem ?? FileSystem.Data;
						if ( entry != null && entry.Type == OldVirtualFileSystem.EntryType.Directory )
						{
							foreach ( var child in realFS.FindDirectory( entry.RealPath ) )
							{
								StandardOutput.WriteLine( $"<DIR> {child}" );
							}
							foreach ( var child in realFS.FindFile( entry.RealPath ) )
							{
								StandardOutput.WriteLine( $"{realFS.FileSize( child ),8} {child}" );
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
					var progEntry = OldVirtualFileSystem.Instance.GetEntry( exePath );
					if ( progEntry != null && progEntry.Type != OldVirtualFileSystem.EntryType.Directory )
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
							ProcessManager.Instance.OpenExecutable( exePath, childOptions );
						}
						catch ( Exception ex )
						{
							StandardOutput.WriteLine( $"Failed to launch '{command}': {ex.Message}" );
						}
					}
					else
					{
						StandardOutput.WriteLine( $"'{command}' is not recognized as an internal or external command, operable program or batch file." );
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
		if ( currentDir.EndsWith( "/" ) )
			return currentDir + input;
		return currentDir + "/" + input;
	}
}
