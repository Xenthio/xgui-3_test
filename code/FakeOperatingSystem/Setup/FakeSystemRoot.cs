using FakeDesktop;
using FakeOperatingSystem;
using Sandbox;
using System.IO;
using System.Threading.Tasks;
using XGUI;

public class FakeSystemRoot
{
	protected static SetupDialog _setupDialog;
	/// <summary>
	/// calls CreateSystemRoot if the systemroot in FileSystem.Data doesn't exist.
	/// </summary>
	public static async Task TryCreateSystemRoot()
	{
		if ( !FileSystem.Data.DirectoryExists( "FakeSystemRoot" ) )
		{
			await CreateSystemRoot();
		}
	}

	public static async Task CreateSystemRoot()
	{
		_setupDialog = new SetupDialog();
		XGUISystem.Instance.Panel.AddChild( _setupDialog );


		await Task.Delay( 20 ); // Give UI time to render

		FileSystem.Data.CreateDirectory( "FakeSystemRoot" );
		//FileSystem.Data.CreateDirectory( "FakeSystemRoot/My Documents" );
		//FileSystem.Data.WriteAllText( "FakeSystemRoot/My Documents/desktop.ini", "[.XGUIInfo]\nIcon=mydocuments\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,3\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=3" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Recycled" );
		FileSystem.Data.WriteAllText( "FakeSystemRoot/Recycled/desktop.ini", "[.XGUIInfo]\nIcon=recyclebinfull\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,10\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=10" );

		SetupRootFiles();

		// Windows
		_setupDialog.UpdateStatus( "Setting up Windows files..." );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows" );
		//FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/All Users" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Downloaded Program Files" );
		FileSystem.Data.WriteAllText( "FakeSystemRoot/Windows/Downloaded Program Files/desktop.ini", "[.XGUIInfo]\nIcon=downloadedprogramfiles\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,5\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=5" );
		//FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Favorites" );
		//FileSystem.Data.WriteAllText( "FakeSystemRoot/Windows/Favorites/desktop.ini", "[.XGUIInfo]\nIcon=favourites\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,2\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=2" );
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

		// System and bundled applications in Windows directory
		SetupWindowsFiles();
		await Task.Delay( 20 ); // Give UI time to render

		// Desktop Folders
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Desktop" );

		_setupDialog.UpdateStatus( "Installing Start Menu Items..." );
		// Start Menu
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Start Menu" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Start Menu/Programs" );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Windows/Start Menu/Start Up" );
		await Task.Delay( 20 ); // Give UI time to render

		_setupDialog.UpdateStatus( "Setting up Program files..." );
		FileSystem.Data.CreateDirectory( "FakeSystemRoot/Program Files" );
		// Program Files with proper application folders
		SetupProgramFiles();

		await Task.Delay( 20 ); // Give UI time to render

		_setupDialog.Complete();
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

		// winver (system application)
		NativeProgram.CompileIntoExe( typeof( WinVerProgram ), $"{windowsDir}/System32/winver.exe" );

		// taskmgr (system application)
		NativeProgram.CompileIntoExe( typeof( TaskMgrProgram ), $"{windowsDir}/System32/taskmgr.exe" );

		// conhost (system application)
		NativeProgram.CompileIntoExe( typeof( ConsoleHostProgram ), $"{windowsDir}/System32/conhost.exe" );

		// cmd (system application)
		NativeProgram.CompileIntoExe( typeof( CommandProgram ), $"{windowsDir}/System32/cmd.exe" );

		// edit (system application)
		NativeProgram.CompileIntoExe( typeof( EditProgram ), $"{windowsDir}/System32/edit.exe" );

		// regedit (system application)
		NativeProgram.CompileIntoExe( typeof( RegEditProgram ), $"{windowsDir}/regedit.exe" );
	}

	/// <summary>
	/// Creates a shortcut file pointing to a target program
	/// </summary>
	private static void CreateShortcut( string shortcutPath, string targetPath, string iconName = "",
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


	[ConCmd( "xguitest_delete_system_root" )]
	public static void DeleteSystemRoot()
	{
		FileSystem.Data.DeleteDirectory( "FakeSystemRoot", true );
	}
}
