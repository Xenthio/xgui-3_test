using FakeDesktop;
using FakeOperatingSystem.OSFileSystem;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace FakeOperatingSystem;
public class UserManager
{
	public List<UserAccount> Users { get; private set; } = new();
	public UserAccount CurrentUser { get; private set; }

	public void LoadUsers()
	{
		// Load from a JSON file or registry (e.g., C:\Documents and Settings\users.json)
		// For now, create a default user if none exist
		if ( !VirtualFileSystem.Instance.FileExists( @"C:\Windows\users.json" ) )
		{
			SaveUsers();
		}
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
		var user = Users.FirstOrDefault( u => u.UserName == username );
		if ( user != null && user.PasswordHash == password ) // Replace with hash check
		{
			CurrentUser = user;
			return true;
		}
		return false;
	}

	public void SetupUserProfile( UserAccount user )
	{
		var vfs = VirtualFileSystem.Instance;

		if ( FakeOSLoader.UserSystemEnabled && user != null )
		{
			// All Users folder
			string allUsers = @"C:\Documents and Settings\All Users";
			if ( !vfs.DirectoryExists( allUsers ) )
				vfs.CreateDirectory( allUsers );

			// User profile root
			if ( !vfs.DirectoryExists( user.ProfilePath ) )
				vfs.CreateDirectory( user.ProfilePath );

			// My Documents
			string myDocs = Path.Combine( user.ProfilePath, "My Documents" );
			if ( !vfs.DirectoryExists( myDocs ) )
				vfs.CreateDirectory( myDocs );

			// Desktop, Start Menu, etc.
			SetupDesktopItems( user );
			SetupStartMenuItems( user );
			SetupQuickLaunch( user );
		}
		else
		{
			// User system disabled: put everything in C:\Windows, My Documents in C:\
			string windows = @"C:\Windows";

			// If this doesn't exist, something is wrong, but let's continue anyways...
			if ( !vfs.DirectoryExists( windows ) )
				vfs.CreateDirectory( windows );

			string myDocs = @"C:\My Documents";
			if ( !vfs.DirectoryExists( myDocs ) )
				vfs.CreateDirectory( myDocs );

			// Setup "global" Desktop, Start Menu, etc. in C:\Windows
			var globalUser = new UserAccount
			{
				ProfilePath = windows
			};
			SetupDesktopItems( globalUser );
			SetupStartMenuItems( globalUser );
			SetupQuickLaunch( globalUser );
		}
	}


	private void SetupStartMenuItems( UserAccount user )
	{
		string startMenuDir = Path.Combine( user.ProfilePath, "Start Menu" );
		VirtualFileSystem.Instance.CreateDirectory( startMenuDir );
		// Internet Explorer shortcut
		CreateShortcut(
			$"{startMenuDir}/Internet Explorer.lnk",
			"C:/Program Files/Internet Explorer/iexplore.exe"
		);
		// Outlook Express shortcut
		CreateShortcut(
			$"{startMenuDir}/Outlook Express.lnk",
			"C:/Program Files/Outlook Express/outlook.exe"
		);
		// Windows Explorer shortcut
		CreateShortcut(
			$"{startMenuDir}/Windows Explorer.lnk",
			"C:/Windows/explorer.exe",
			"explore"
		);
		// Command Prompt shortcut
		CreateShortcut(
			$"{startMenuDir}/Command Prompt.lnk",
			"C:/Windows/System32/cmd.exe"
		);

		// Accessories folder
		string accessoriesDir = $"{startMenuDir}/Accessories";
		VirtualFileSystem.Instance.CreateDirectory( accessoriesDir );
		// Notepad shortcut
		CreateShortcut(
			$"{accessoriesDir}/Notepad.lnk",
			"C:/Windows/notepad.exe"
		);
		// Paint shortcut
		CreateShortcut(
			$"{accessoriesDir}/Paint.lnk",
			"C:/Windows/mspaint.exe"
		);
		// Calculator shortcut
		CreateShortcut(
			$"{accessoriesDir}/Calculator.lnk",
			"C:/Windows/calc.exe"
		);
		// Task Manager shortcut
		CreateShortcut(
			$"{accessoriesDir}/Task Manager.lnk",
			"C:/Windows/System32/taskmgr.exe"
		);

		// Games folder
		string gamesDir = $"{accessoriesDir}/Games";
		VirtualFileSystem.Instance.CreateDirectory( gamesDir );
		// Minesweeper shortcut
		CreateShortcut(
			$"{gamesDir}/Minesweeper.lnk",
			"C:/Windows/System32/winmine.exe"
		);

		// Ultimate Doom for Windows 95 shortcut
		string doomDir = $"{startMenuDir}/Ultimate Doom for Windows 95";
		VirtualFileSystem.Instance.CreateDirectory( doomDir );
		CreateShortcut(
			$"{doomDir}/Doom95.lnk",
			"C:/Program Files/Ultimate Doom for Windows 95/doom95.exe"
		);
	}
	private void SetupQuickLaunch( UserAccount User )
	{
		//string quickLaunchDir = "FakeSystemRoot/Windows/Application Data/Microsoft/Internet Explorer/Quick Launch";
		string quickLaunchDir = Path.Combine( User.ProfilePath, "Application Data", "Microsoft", "Internet Explorer", "Quick Launch" );
		VirtualFileSystem.Instance.CreateDirectory( quickLaunchDir );
		// Show Desktop shortcut
		CreateShortcut(
			$"{quickLaunchDir}/Show Desktop.lnk",
			"C:/Windows/System32/Show Desktop.scf",
			"Show Desktop"
		);
		// Internet Explorer shortcut
		CreateShortcut(
			$"{quickLaunchDir}/Internet Explorer.lnk",
			"C:/Program Files/Internet Explorer/Iexplore.exe"
		);
		// Outlook Express shortcut
		CreateShortcut(
			$"{quickLaunchDir}/Outlook Express.lnk",
			"C:/Program Files/Outlook Express/outlook.exe"
		);
	}

	public void SetupDesktopItems( UserAccount user )
	{
		//string desktopDir = "FakeSystemRoot/Windows/Desktop";
		string desktopDir = Path.Combine( user.ProfilePath, "Desktop" );
		VirtualFileSystem.Instance.CreateDirectory( desktopDir );
		VirtualFileSystem.Instance.CreateDirectory( $"{desktopDir}/Online Services" );
		// Outlook Express shortcut
		CreateShortcut(
			$"{desktopDir}/Outlook Express.lnk",
			"C:/Program Files/Outlook Express/outlook.exe"
		);
		// Doom 95 shortcut
		CreateShortcut(
			$"{desktopDir}/Doom 95.lnk",
			"C:/Program Files/Ultimate Doom for Windows 95/doom95.exe"
		);
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
		VirtualFileSystem.Instance.WriteAllText(
			shortcutPath,
			shortcut.ToFileContent()
		);
	}
}
