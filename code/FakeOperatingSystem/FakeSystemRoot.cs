using FakeDesktop;
using FakeOperatingSystem;
using Sandbox;
using System.IO;

public class FakeSystemRoot
{
	/// <summary>
	/// calls CreateSystemRoot if the systemroot in FileSystem.Data doesn't exist.
	/// </summary>
	public static void TryCreateSystemRoot()
	{
		if ( !FileSystem.Data.DirectoryExists( "FakeSystemRoot" ) )
		{
			CreateSystemRoot();
		}
	}

	public static void CreateSystemRoot()
	{
		FileSystem.Data.CreateDirectory( "FakeSystemRoot" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Program Files" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/My Documents" );

		// Windows
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Fonts" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/System" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/System32" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/System32/drivers" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/System32/config" );

		// Desktop Folders
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Desktop" );

		// Start Menu
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Start Menu" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Start Menu/Programs" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Start Menu/Start Up" );

		// Program Files with proper application folders
		SetupProgramFiles();

		// System and bundled applications in Windows directory
		SetupWindowsFiles();

		// Create desktop shortcuts and other items
		CreateDefaultDesktopItems();
	}

	/// <summary>
	/// Create program folders and files in Program Files directory
	/// </summary>
	private static void SetupProgramFiles()
	{
		string programFilesDir = "FakeSystemRoot/Program Files";

		// Internet Explorer folder
		string ieDir = $"{programFilesDir}/Internet Explorer";
		FileSystem.Data.CreateDirectory( ieDir );
		CreateProgramFile(
			$"{ieDir}/iexplore.exe",
			new ProgramDescriptor(
				"Internet Explorer",
				"iexplore.exe",
				"iexplore",
				"InternetExplorer"
			)
		);

		// Ultimate Doom for Windows 95
		string doomDir = $"{programFilesDir}/Ultimate Doom for Windows 95";
		FileSystem.Data.CreateDirectory( doomDir );
		CreateProgramFile(
			$"{doomDir}/doom95.exe",
			new ProgramDescriptor(
				"Doom 95",
				"doom95.exe",
				"doom95",
				"Wad"
			)
		);

		// Outlook Express
		string outlookDir = $"{programFilesDir}/Outlook Express";
		FileSystem.Data.CreateDirectory( outlookDir );
		CreateProgramFile(
			$"{outlookDir}/outlook.exe",
			new ProgramDescriptor(
				"Outlook Express",
				"outlook.exe",
				"outlook",
				"OutlookExpress"
			)
		);

		// Steam (modern app not in Windows 98, but included for fun)
		string steamDir = $"{programFilesDir}/Steam";
		FileSystem.Data.CreateDirectory( steamDir );
		CreateProgramFile(
			$"{steamDir}/steam.exe",
			new ProgramDescriptor(
				"Steam",
				"steam.exe",
				"steam",
				"GameLauncher"
			)
		);
	}

	/// <summary>
	/// Setup applications that belong in the Windows directory
	/// </summary>
	private static void SetupWindowsFiles()
	{
		string windowsDir = "FakeSystemRoot/Windows";

		// Windows Explorer (system application)
		CreateProgramFile(
			$"{windowsDir}/explorer.exe",
			new ProgramDescriptor(
				"Windows Explorer",
				"explorer.exe",
				"explorer",
				"Explorer"
			)
		);

		// Notepad (system application)
		NativeProgram.CompileIntoExe( typeof( NotepadProgram ), $"{windowsDir}/notepad.exe" );

		// Paint (system application)
		CreateProgramFile(
			$"{windowsDir}/mspaint.exe",
			new ProgramDescriptor(
				"Paint",
				"mspaint.exe",
				"paint",
				"Paint"
			)
		);
	}

	public static void CreateDefaultDesktopItems()
	{
		string desktopDir = "FakeSystemRoot/Windows/Desktop";
		FileSystem.Data.CreateDirectory( $"{desktopDir}/Online Services" );
		// Outlook Express shortcut
		CreateShortcut(
			$"{desktopDir}/Outlook Express.lnk",
			"FakeSystemRoot/Program Files/Outlook Express/outlook.exe",
			"outlook"
		);
		// Doom 95 shortcut
		CreateShortcut(
			$"{desktopDir}/Doom 95.lnk",
			"FakeSystemRoot/Program Files/Ultimate Doom for Windows 95/doom95.exe",
			"doom95"
		);
	}

	/// <summary>
	/// Creates a program exe file with embedded panel information
	/// </summary>
	private static void CreateProgramFile( string programPath, ProgramDescriptor program )
	{
		FakeExecutable.CreateFakeExe( programPath, program );
	}

	/// <summary>
	/// Creates a shortcut file pointing to a target program
	/// </summary>
	private static void CreateShortcut( string shortcutPath, string targetPath, string iconName,
									 string arguments = "", string workingDir = "" )
	{
		// Create the shortcut descriptor
		var shortcut = new ShortcutDescriptor(
			targetPath,
			string.IsNullOrEmpty( workingDir ) ? Path.GetDirectoryName( targetPath ) : workingDir,
			arguments,
			iconName
		);

		// Write to file
		FileSystem.Data.WriteAllText(
			shortcutPath,
			shortcut.ToFileContent()
		);
	}


	[ConCmd( "xguitest_force_recreate_system_root" )]
	public static void ForceRecreateSystemRoot()
	{
		FileSystem.Data.DeleteDirectory( "FakeSystemRoot", true );
		CreateSystemRoot();
	}
}
