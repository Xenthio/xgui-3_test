using FakeDesktop;
using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.User;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XGUI;

namespace FakeOperatingSystem;
public class UserManager
{
	public List<UserAccount> Users { get; private set; } = new();
	public UserAccount CurrentUser { get; private set; }

	public void LoadUsers()
	{
		if ( !VirtualFileSystem.Instance.FileExists( @"C:\Windows\users.json" ) )
			return;
		else
		{
			var json = VirtualFileSystem.Instance.ReadAllText( @"C:\Windows\users.json" );
			Users = System.Text.Json.JsonSerializer.Deserialize<List<UserAccount>>( json ) ?? new();
		}
	}

	public void SaveUsers()
	{
		var json = System.Text.Json.JsonSerializer.Serialize( Users, new System.Text.Json.JsonSerializerOptions { WriteIndented = true } );
		VirtualFileSystem.Instance.WriteAllText( @"C:\Windows\users.json", json );
	}

	public bool Login( string username, string password )
	{
		var user = Users.FirstOrDefault( u => u.UserName.Equals( username, System.StringComparison.OrdinalIgnoreCase ) );
		// TODO: Implement proper password hashing and comparison
		if ( user != null && (string.IsNullOrEmpty( user.PasswordHash ) || user.PasswordHash == password) )
		{
			CurrentUser = user;
			return true;
		}
		return false;
	}

	/// <summary>
	/// Sets up the user's profile folders asynchronously. This allows a UI 
	/// to show a "Setting up personalized settings..." message while this method executes.
	/// </summary>
	public async Task SetupUserProfile( UserAccount user )
	{
		PersonalisedSettingsDialog personalizedDialog = new PersonalisedSettingsDialog();
		XGUISystem.Instance.Panel.AddChild( personalizedDialog );
		await Task.Delay( 50 ); // Give the dialog a moment to render

		var vfs = VirtualFileSystem.Instance;
		string effectiveProfilePath;

		if ( FakeOSLoader.UserSystemEnabled && user != null )
		{
			personalizedDialog.UpdateStatus( $"Loading personal settings for {user.UserName}..." );
			await Task.Delay( 200 ); // Simulate work
			effectiveProfilePath = user.ProfilePath;
			if ( !vfs.DirectoryExists( effectiveProfilePath ) )
				vfs.CreateDirectory( effectiveProfilePath );
			await Task.Yield();

			personalizedDialog.UpdateStatus( "Applying your personal settings..." );
			await Task.Delay( 200 );

			string myDocs = Path.Combine( effectiveProfilePath, "My Documents" );
			if ( !vfs.DirectoryExists( myDocs ) )
			{
				vfs.CreateDirectory( myDocs );
				vfs.WriteAllText( Path.Combine( myDocs, "desktop.ini" ),
					"[.XGUIInfo]\nIcon=mydocuments\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,3\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=3" );
			}
			await Task.Yield();

			string favouritesDir = Path.Combine( effectiveProfilePath, "Favorites" );
			if ( !vfs.DirectoryExists( favouritesDir ) )
			{
				vfs.CreateDirectory( favouritesDir );
				vfs.WriteAllText( Path.Combine( favouritesDir, "desktop.ini" ),
					"[.XGUIInfo]\nIcon=favourites\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,2\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=2" );
			}
			await Task.Yield();

			await CreateStandardUserFolders( effectiveProfilePath, vfs, personalizedDialog ); // Pass dialog
			await SetupStartMenuItems( user, personalizedDialog ); // Pass dialog
			await SetupQuickLaunch( user, personalizedDialog );   // Pass dialog
			await SetupDesktopItems( user, personalizedDialog );  // Pass dialog
		}
		else
		{
			personalizedDialog.UpdateStatus( "Applying system settings..." );
			await Task.Delay( 200 );
			effectiveProfilePath = UserProfileHelper.GetProfilePath();
			if ( !vfs.DirectoryExists( effectiveProfilePath ) )
				vfs.CreateDirectory( effectiveProfilePath );
			await Task.Yield();

			string myDocsGlobal = UserProfileHelper.GetMyDocumentsPath();
			if ( !vfs.DirectoryExists( myDocsGlobal ) )
			{
				vfs.CreateDirectory( myDocsGlobal );
				vfs.WriteAllText( Path.Combine( myDocsGlobal, "desktop.ini" ),
					"[.XGUIInfo]\nIcon=mydocuments\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,3\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=3" );
			}
			await Task.Yield();

			string favouritesGlobal = UserProfileHelper.GetFavoritesPath();
			if ( !vfs.DirectoryExists( favouritesGlobal ) )
			{
				vfs.CreateDirectory( favouritesGlobal );
				vfs.WriteAllText( Path.Combine( favouritesGlobal, "desktop.ini" ),
					"[.XGUIInfo]\nIcon=favourites\n\n[.ShellClassInfo]\nIconResource=C:\\WINDOWS\\system32\\shell32.dll,2\nIconFile=C:\\WINDOWS\\system32\\shell32.dll\nIconIndex=2" );
			}
			await Task.Yield();

			var globalUserAccountRepresentation = new UserAccount { ProfilePath = effectiveProfilePath };
			await CreateStandardUserFolders( effectiveProfilePath, vfs, personalizedDialog );
			await SetupStartMenuItems( globalUserAccountRepresentation, personalizedDialog );
			await SetupQuickLaunch( globalUserAccountRepresentation, personalizedDialog );
			await SetupDesktopItems( globalUserAccountRepresentation, personalizedDialog );
		}

		personalizedDialog.UpdateStatus( "Finalizing settings..." );
		await Task.Delay( 300 ); // Simulate final steps
		personalizedDialog.Complete();
	}

	private async Task CreateStandardUserFolders( string baseProfilePath, IVirtualFileSystem vfs, PersonalisedSettingsDialog dialog )
	{
		dialog.UpdateStatus( "Creating application data folders..." );
		await Task.Delay( 50 );
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Application Data" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Application Data", "Microsoft" ) ); await Task.Yield();

		dialog.UpdateStatus( "Setting up user environment folders..." );
		await Task.Delay( 50 );
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Cookies" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "History" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Local Settings" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Local Settings", "Application Data" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Local Settings", "Temp" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Local Settings", "Temporary Internet Files" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "NetHood" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "PrintHood" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Recent" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "SendTo" ) ); await Task.Yield();
		vfs.CreateDirectory( Path.Combine( baseProfilePath, "Templates" ) ); await Task.Yield();
	}

	private async Task SetupStartMenuItems( UserAccount user, PersonalisedSettingsDialog dialog )
	{
		dialog.UpdateStatus( "Setting up Start Menu items..." );
		await Task.Delay( 200 );

		string startMenuRoot = Path.Combine( user.ProfilePath, "Start Menu" );
		VirtualFileSystem.Instance.CreateDirectory( startMenuRoot ); await Task.Yield();
		string programsDir = Path.Combine( startMenuRoot, "Programs" );
		VirtualFileSystem.Instance.CreateDirectory( programsDir ); await Task.Yield();

		CreateShortcut( Path.Combine( programsDir, "Internet Explorer.lnk" ), "C:/Program Files/Internet Explorer/iexplore.exe" ); await Task.Yield();
		CreateShortcut( Path.Combine( programsDir, "Outlook Express.lnk" ), "C:/Program Files/Outlook Express/outlook.exe" ); await Task.Yield();
		CreateShortcut( Path.Combine( programsDir, "Windows Explorer.lnk" ), "C:/Windows/explorer.exe", "explore" ); await Task.Yield();
		CreateShortcut( Path.Combine( programsDir, "Command Prompt.lnk" ), "C:/Windows/System32/cmd.exe" ); await Task.Yield();

		string accessoriesDir = Path.Combine( programsDir, "Accessories" );
		VirtualFileSystem.Instance.CreateDirectory( accessoriesDir ); await Task.Yield();
		CreateShortcut( Path.Combine( accessoriesDir, "Notepad.lnk" ), "C:/Windows/notepad.exe" ); await Task.Yield();
		CreateShortcut( Path.Combine( accessoriesDir, "Paint.lnk" ), "C:/Windows/mspaint.exe" ); await Task.Yield();
		CreateShortcut( Path.Combine( accessoriesDir, "Calculator.lnk" ), "C:/Windows/calc.exe" ); await Task.Yield();
		CreateShortcut( Path.Combine( accessoriesDir, "Task Manager.lnk" ), "C:/Windows/System32/taskmgr.exe" ); await Task.Yield();

		string gamesDir = Path.Combine( accessoriesDir, "Games" );
		VirtualFileSystem.Instance.CreateDirectory( gamesDir ); await Task.Yield();
		CreateShortcut( Path.Combine( gamesDir, "Minesweeper.lnk" ), "C:/Windows/System32/winmine.exe" ); await Task.Yield();

		string doomProgramsDir = Path.Combine( programsDir, "Ultimate Doom for Windows 95" );
		VirtualFileSystem.Instance.CreateDirectory( doomProgramsDir ); await Task.Yield();
		CreateShortcut( Path.Combine( doomProgramsDir, "Doom95.lnk" ), "C:/Program Files/Ultimate Doom for Windows 95/doom95.exe" ); await Task.Yield();

		string startupDir = Path.Combine( startMenuRoot, "StartUp" );
		VirtualFileSystem.Instance.CreateDirectory( startupDir ); await Task.Yield();
	}
	private async Task SetupQuickLaunch( UserAccount user, PersonalisedSettingsDialog dialog )
	{
		dialog.UpdateStatus( "Configuring Quick Launch bar..." );
		await Task.Delay( 50 );
		string quickLaunchDir = Path.Combine( user.ProfilePath, "Application Data", "Microsoft", "Internet Explorer", "Quick Launch" );
		VirtualFileSystem.Instance.CreateDirectory( quickLaunchDir ); await Task.Yield();
		CreateShortcut( Path.Combine( quickLaunchDir, "Show Desktop.lnk" ), "C:/Windows/System32/Show Desktop.scf", "Show Desktop" ); await Task.Yield();
		CreateShortcut( Path.Combine( quickLaunchDir, "Internet Explorer.lnk" ), "C:/Program Files/Internet Explorer/Iexplore.exe" ); await Task.Yield();
		CreateShortcut( Path.Combine( quickLaunchDir, "Outlook Express.lnk" ), "C:/Program Files/Outlook Express/outlook.exe" ); await Task.Yield();
	}

	public async Task SetupDesktopItems( UserAccount user, PersonalisedSettingsDialog dialog )
	{
		dialog.UpdateStatus( "Preparing your Desktop..." );
		await Task.Delay( 100 );
		string desktopDir = Path.Combine( user.ProfilePath, "Desktop" );
		VirtualFileSystem.Instance.CreateDirectory( desktopDir ); await Task.Yield();
		VirtualFileSystem.Instance.CreateDirectory( Path.Combine( desktopDir, "Online Services" ) ); await Task.Yield();
		CreateShortcut( Path.Combine( desktopDir, "Outlook Express.lnk" ), "C:/Program Files/Outlook Express/outlook.exe" ); await Task.Yield();
		CreateShortcut( Path.Combine( desktopDir, "Doom 95.lnk" ), "C:/Program Files/Ultimate Doom for Windows 95/doom95.exe" ); await Task.Yield();
	}

	// CreateShortcut remains synchronous as VFS operations are synchronous
	private static void CreateShortcut( string shortcutPath, string targetPath, string iconName = "",
									 string arguments = "", string workingDir = "" )
	{
		var shortcut = new ShortcutDescriptor(
			targetPath,
			string.IsNullOrEmpty( workingDir ) ? Path.GetDirectoryName( targetPath ) : workingDir,
			arguments,
			iconName
		);
		VirtualFileSystem.Instance.WriteAllText(
			shortcutPath,
			shortcut.ToFileContent()
		);
	}
}
