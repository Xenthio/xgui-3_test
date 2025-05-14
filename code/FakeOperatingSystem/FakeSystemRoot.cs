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
		FileSystem.Data.WriteAllText( "FakeSystemRoot/My Documents/desktop.ini", "[.XGUIInfo]\nIcon=mydocuments\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,3\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=3" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Recycled" );
		FileSystem.Data.WriteAllText( "FakeSystemRoot/Recycled/desktop.ini", "[.XGUIInfo]\nIcon=recyclebinfull\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,10\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=10" );

		SetupRootFiles();

		// Windows
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/All Users" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Downloaded Program Files" );
		FileSystem.Data.WriteAllText( "FakeSystemRoot/Windows/Downloaded Program Files/desktop.ini", "[.XGUIInfo]\nIcon=downloadedprogramfiles\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,5\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=5" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Favorites" );
		FileSystem.Data.WriteAllText( "FakeSystemRoot/Windows/Favorites/desktop.ini", "[.XGUIInfo]\nIcon=favourites\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,2\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=2" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Fonts" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Help" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/History" );
		FileSystem.Data.WriteAllText( "FakeSystemRoot/Windows/History/desktop.ini", "[.XGUIInfo]\nIcon=history\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,4\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=4" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Media" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Offline Web Pages" );
		FileSystem.Data.WriteAllText( "FakeSystemRoot/Windows/Offline Web Pages/desktop.ini", "[.XGUIInfo]\nIcon=offlinepages\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\webcheck.dll,1\nIconFile=C:\\WINDOWS\\system32\\webcheck.dll\nIconIndex=1" );
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

		// Quick Launch folder
		SetupQuickLaunch();

		// Create desktop shortcuts and other items
		CreateDefaultDesktopItems();
	}

	public static void SetupRootFiles()
	{
		string rootDir = "FakeSystemRoot";
		FileSystem.Data.WriteAllText( $"{rootDir}/Autoexec.bat", "@echo off\n win" );
		FileSystem.Data.WriteAllText( $"{rootDir}/Config.sys", "DEVICE=C:\\WINDOWS\\HIMEM.SYS\nDEVICE=C:\\WINDOWS\\EMM386.EXE" );
		FileSystem.Data.WriteAllText( $"{rootDir}/MSDOS.SYS", "[Paths]\nWinDir=C:\\WINDOWS\nWinBootDir=C:\\WINDOWS\nHostWinBootDrv=C\n\n[Options]\nBootMulti=1\nBootGUI=1\nDoubleBuffer=1\nAutoScan=1\nWinVer=4.10.2222" );
		FileSystem.Data.WriteAllText( $"{rootDir}/boot.ini", "[boot loader]\nTimeout=30\nDefault=multi(0)disk(0)rdisk(0)partition(1)\\WINDOWS\n[operating systems]\nmulti(0)disk(0)rdisk(0)partition(1)\\WINDOWS=\"Microsoft Windows 98 Hybrid NT Edition\" /fastdetect" );
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
		NativeProgram.CompileIntoExe( typeof( IExploreProgram ), $"{ieDir}/Iexplore.exe" );

		// Ultimate Doom for Windows 95
		string doomDir = $"{programFilesDir}/Ultimate Doom for Windows 95";
		FileSystem.Data.CreateDirectory( doomDir );
		NativeProgram.CompileIntoExe( typeof( Doom95Program ), $"{doomDir}/doom95.exe" );

		// Outlook Express
		string outlookDir = $"{programFilesDir}/Outlook Express";
		NativeProgram.CompileIntoExe( typeof( OutlookExpressProgram ), $"{outlookDir}/outlook.exe" );

		// Steam (modern app not in Windows 98, but included for fun)
		string steamDir = $"{programFilesDir}/Steam";
		FileSystem.Data.CreateDirectory( steamDir );
		NativeProgram.CompileIntoExe( typeof( SteamProgram ), $"{steamDir}/steam.exe" );
	}

	private static void SetupQuickLaunch()
	{
		string quickLaunchDir = "FakeSystemRoot/Windows/Application Data/Microsoft/Internet Explorer/Quick Launch";
		FileSystem.Data.CreateDirectory( quickLaunchDir );
		// Show Desktop shortcut
		CreateShortcut(
			$"{quickLaunchDir}/Show Desktop.lnk",
			"C:/Windows/System32/Show Desktop.scf",
			"Show Desktop"
		);
		// Internet Explorer shortcut
		CreateShortcut(
			$"{quickLaunchDir}/Internet Explorer.lnk",
			"C:/Program Files/Internet Explorer/Iexplore.exe",
			"iexplore"
		);
		// Outlook Express shortcut
		CreateShortcut(
			$"{quickLaunchDir}/Outlook Express.lnk",
			"C:/Program Files/Outlook Express/outlook.exe",
			"outlook"
		);
	}

	/// <summary>
	/// Setup applications that belong in the Windows directory
	/// </summary>
	private static void SetupWindowsFiles()
	{
		string windowsDir = "FakeSystemRoot/Windows";

		// Windows Explorer (system application)
		NativeProgram.CompileIntoExe( typeof( ExplorerProgram ), $"{windowsDir}/explorer.exe" );

		// Notepad (system application)
		NativeProgram.CompileIntoExe( typeof( NotepadProgram ), $"{windowsDir}/notepad.exe" );

		// Paint (system application)
		NativeProgram.CompileIntoExe( typeof( PaintProgram ), $"{windowsDir}/mspaint.exe" );

		// taskmgr (system application)
		NativeProgram.CompileIntoExe( typeof( TaskMgrProgram ), $"{windowsDir}/System32/taskmgr.exe" );
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
