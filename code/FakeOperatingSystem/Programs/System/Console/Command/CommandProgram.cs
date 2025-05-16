using FakeOperatingSystem;
using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.Utils;
using Sandbox;
using System;
using System.Linq;

/// <summary>
/// Mimics the behaviour of cmd.exe (MS-DOS Style Command Prompt) in Windows.
/// </summary>
public class CommandProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/System32/cmd.exe";
	public override bool ConsoleApp => true;
	public string cd = @"C:\";
	public string Prompt = "$P$G";
	public bool EchoEnabled = true;

	BaseProcess process;

	public override void Main( NativeProcess process, Win32LaunchOptions launchOptions = null )
	{
		if ( !String.IsNullOrWhiteSpace( launchOptions?.WorkingDirectory ) )
			cd = launchOptions.WorkingDirectory;
		this.process = process;
		StandardOutput.WriteLine( "" );
		StandardOutput.WriteLine( "" );
		StandardOutput.WriteLine( $"{FakeOSLoader.VersionString}" );
		StandardOutput.WriteLine( "   FakeOS Command Prompt [Type 'help' for commands]" );

		while ( true )
		{
			if ( EchoEnabled ) StandardOutput.Write( GetFormattedPrompt() );
			var line = StandardInput.ReadLine();
			if ( line == null )
				continue;

			if ( EchoEnabled ) StandardOutput.WriteLine( line );

			var commandLine = line.Trim();
			if ( string.IsNullOrEmpty( commandLine ) )
				continue;

			var parts = commandLine.Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries );
			var command = parts[0].ToLowerInvariant();
			var args = parts.Length > 1 ? parts[1] : "";
			ParseCommand( command, args );
			StandardOutput.WriteLine( "" );
		}
	}

	public string GetFormattedPrompt()
	{
		string formattedPrompt = Prompt;
		formattedPrompt = formattedPrompt.Replace( "$Q", "=" );
		formattedPrompt = formattedPrompt.Replace( "$$", "$" );
		formattedPrompt = formattedPrompt.Replace( "$T", $"{DateTime.Now.ToString( "HH:mm:ss" )}" );
		formattedPrompt = formattedPrompt.Replace( "$D", $"{DateTime.Now.ToString( "dd/MM/yyyy" )}" );
		formattedPrompt = formattedPrompt.Replace( "$P", cd );
		formattedPrompt = formattedPrompt.Replace( "$V,", FakeOSLoader.VersionString );
		formattedPrompt = formattedPrompt.Replace( "$N", VirtualFileSystem.Instance.ResolveMountPoint( cd ).MountPoint.Name );
		formattedPrompt = formattedPrompt.Replace( "$G", ">" );
		formattedPrompt = formattedPrompt.Replace( "$L", "<" );
		formattedPrompt = formattedPrompt.Replace( "$B", "|" );
		formattedPrompt = formattedPrompt.Replace( "$_", "\n" );
		formattedPrompt = formattedPrompt.Replace( "$E", "\u001B" ); // Escape character
		formattedPrompt = formattedPrompt.Replace( "$H", "\u0008" ); // Backspace character
		formattedPrompt = formattedPrompt.Replace( "$A", "&" );
		formattedPrompt = formattedPrompt.Replace( "$C", "(" );
		formattedPrompt = formattedPrompt.Replace( "$F", ")" );
		formattedPrompt = formattedPrompt.Replace( "$S", " " );

		return formattedPrompt;
	}

	public void ParseCommand( string command, string args )
	{

		switch ( command )
		{
			case "exit":
				StandardOutput.WriteLine( "Exiting..." );
				return;

			case "echo":
				if ( args == "on" )
				{
					EchoEnabled = true;
					return;
				}
				else if ( args == "off" )
				{
					EchoEnabled = false;
					return;
				}
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

			case "cd..":
			case "cd.":
			case "cd":
				if ( string.IsNullOrWhiteSpace( args ) )
				{
					if ( command == "cd.." )
					{
						args = "..";
					}
					else if ( command == "cd." )
					{
						args = ".";
					}
					else
					{
						// No args, just print the current directory
						StandardOutput.WriteLine( cd );
						return;
					}
				}

				string newDir = ResolvePath( cd, args );
				var entry = VirtualFileSystem.Instance.DirectoryExists( newDir );
				if ( args == ".." )
				{
					var upDir = VirtualFileSystem.Instance.GetDirectoryName( cd );
					if ( upDir == null )
					{
						return;
					}
					newDir = FormatDir( upDir );
				}
				else if ( args == "." )
				{
					return;
				}
				if ( VirtualFileSystem.Instance.DirectoryExists( newDir ) )
				{
					cd = newDir;
				}
				else
				{
					StandardOutput.WriteLine( $"The system cannot find the path specified." );
				}
				break;

			case "dir":
				{
					if ( VirtualFileSystem.Instance.DirectoryExists( cd ) )
					{
						// Volume in drive C has no label.
						// Volume Serial Number is 0000-0001
						//
						// Directory of C:\blah
						//

						var drive = VirtualFileSystem.Instance.ResolveMountPoint( cd ).MountPoint;
						if ( drive != null )
						{
							if ( string.IsNullOrEmpty( drive.Label ) )
							{
								StandardOutput.WriteLine( $" Volume in drive {drive.Name} has no label." );
							}
							else
							{
								StandardOutput.WriteLine( $" Volume in drive {drive.Name} is {drive.Label}." );
							}
							StandardOutput.WriteLine( $" Volume Serial Number is 0000-0000" );
						}

						StandardOutput.WriteLine( "" );
						StandardOutput.WriteLine( $" Directory of {FormatDir( cd )}" );
						StandardOutput.WriteLine( "" );

						foreach ( var child in VirtualFileSystem.Instance.GetDirectories( cd ) )
						{
							var dirName = VirtualFileSystem.Instance.GetFileName( child );

							// 28/09/2024  09:31 PM    <DIR>          Folder
							var time = VirtualFileSystem.Instance.ModifiedDate( child );
							var dateTime = DateTime.FromFileTime( time );
							var formattedDate = dateTime.ToString( "dd/MM/yyyy  hh:mm tt" );

							StandardOutput.WriteLine( $"{formattedDate,8}{"<DIR>",8} {"",8} {dirName}" );
						}
						foreach ( var child in VirtualFileSystem.Instance.GetFiles( cd ) )
						{
							var fileName = VirtualFileSystem.Instance.GetFileName( child );

							// 28/09/2024  09:31 PM        51,118,080 File.png
							var time = VirtualFileSystem.Instance.ModifiedDate( child );
							var dateTime = DateTime.FromFileTime( time );
							var formattedDate = dateTime.ToString( "dd/MM/yyyy  hh:mm tt" );

							StandardOutput.WriteLine( $"{formattedDate,8} {VirtualFileSystem.Instance.FileSize( child ).ToString( "N0" ),16} {fileName}" );
						}
						// 25 File( s )     54,260,514 bytes
						// 76 Dir( s )  22,052,622,336 bytes free
						var files = VirtualFileSystem.Instance.GetFiles( cd );
						var directories = VirtualFileSystem.Instance.GetDirectories( cd );
						StandardOutput.WriteLine( $"{files.Count().ToString( "N0" ),15} File(s) {files.Sum( f => VirtualFileSystem.Instance.FileSize( f ) ).ToString( "N0" ),14} bytes" );
						StandardOutput.WriteLine( $"{directories.Count().ToString( "N0" ),15} Dir(s)  {VirtualFileSystem.Instance.GetFreeSpace( cd ).ToString( "N0" ),14} bytes free" );

					}
					else
					{
						StandardOutput.WriteLine( "Directory not found." );
					}
				}
				break;

			case "ver":
				StandardOutput.WriteLine( "" );
				StandardOutput.WriteLine( $"{FakeOSLoader.VersionString}" );
				break;

			case "cls":
				// Clear the console
				StandardOutput.WriteLine( "\u001b[2J\u001b[H" );
				break;

			case "mkdir":
				VirtualFileSystem.Instance.CreateDirectory( ResolvePath( cd, args ) );
				break;

			case "rmdir":
				if ( VirtualFileSystem.Instance.DirectoryExists( ResolvePath( cd, args ) ) )
				{
					VirtualFileSystem.Instance.DeleteDirectory( ResolvePath( cd, args ) );
				}
				else
				{
					StandardOutput.WriteLine( $"The system cannot find the path specified." );
				}
				break;

			case "del":
				if ( VirtualFileSystem.Instance.FileExists( ResolvePath( cd, args ) ) )
				{
					VirtualFileSystem.Instance.DeleteFile( ResolvePath( cd, args ) );
				}
				else
				{
					StandardOutput.WriteLine( $"The system cannot find the file specified." );
				}
				break;

			case "copy":
				var parts = args.Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries );
				if ( parts.Length < 2 )
				{
					StandardOutput.WriteLine( "The syntax of the command is incorrect." );
					break;
				}
				var source = ResolvePath( cd, parts[0] );
				var dest = ResolvePath( cd, parts[1] );
				if ( VirtualFileSystem.Instance.FileExists( source ) )
				{
					VirtualFileSystem.Instance.CopyFile( source, dest );
				}
				else
				{
					StandardOutput.WriteLine( $"The system cannot find the file specified." );
				}
				break;

			case "ren":
			case "rename":
			case "move":
				parts = args.Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries );
				if ( parts.Length < 2 )
				{
					StandardOutput.WriteLine( "The syntax of the command is incorrect." );
					break;
				}
				source = ResolvePath( cd, parts[0] );
				dest = ResolvePath( cd, parts[1] );
				if ( VirtualFileSystem.Instance.FileExists( source ) )
				{
					VirtualFileSystem.Instance.MoveFile( source, dest );
				}
				else
				{
					StandardOutput.WriteLine( $"The system cannot find the file specified." );
				}
				break;

			case "type":
				if ( string.IsNullOrWhiteSpace( args ) )
				{
					StandardOutput.WriteLine( "The syntax of the command is incorrect." );
					break;
				}
				if ( VirtualFileSystem.Instance.FileExists( ResolvePath( cd, args ) ) )
				{
					var fileContents = VirtualFileSystem.Instance.ReadAllBytes( ResolvePath( cd, args ) );
					var text = DOSEncodingHelper.GetStringCp437KeepControl( fileContents );

					// write char by char
					foreach ( var c in text )
					{
						if ( c == '\u001A' )
						{
							break;
						}
						StandardOutput.Write( c );
					}
				}
				else
				{
					StandardOutput.WriteLine( $"The system cannot find the file specified." );
				}
				break;

			case "start":
				break;

			case "prompt":
				if ( string.IsNullOrWhiteSpace( args ) )
				{
					Prompt = "$P$G";
				}
				else
				{
					Prompt = args;
				}
				break;

			case "pause":
				Pause();
				break;

			case "title":
				StandardOutput.Write( "\u001B]0;" + args + "\u0007" );
				break;

			// Search in cwd and path for operable programs or batch files
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
							WorkingDirectory = cd,
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

	private void Pause()
	{
		StandardOutput.Write( "Press any key to continue . . . " );
		// Wait for a key press
		var k = StandardInput.Read();
		StandardOutput.WriteLine( "" );
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
