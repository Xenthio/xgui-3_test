using FakeOperatingSystem.OSFileSystem;
using System.Collections.Generic;
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
		if ( !VirtualFileSystem.Instance.FileExists( @"C:\Documents and Settings\users.json" ) )
		{
			var defaultUser = new UserAccount
			{
				UserName = "Default",
				PasswordHash = "", // No password
				ProfilePath = @"C:\Documents and Settings\Default\",
				RegistryHivePath = @"C:\Documents and Settings\Default\NTUSER.DAT"
			};
			Users.Add( defaultUser );
			SaveUsers();
		}
		else
		{
			var json = VirtualFileSystem.Instance.ReadAllText( @"C:\Documents and Settings\users.json" );
			Users = System.Text.Json.JsonSerializer.Deserialize<List<UserAccount>>( json ) ?? new();
		}
	}

	public void SaveUsers()
	{
		var json = System.Text.Json.JsonSerializer.Serialize( Users, new System.Text.Json.JsonSerializerOptions { WriteIndented = true } );
		VirtualFileSystem.Instance.WriteAllText( @"C:\Documents and Settings\users.json", json );
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

	public void EnsureProfileFolders( UserAccount user )
	{
		var vfs = VirtualFileSystem.Instance;
		if ( !vfs.DirectoryExists( user.ProfilePath ) )
			vfs.CreateDirectory( user.ProfilePath );
		if ( !vfs.DirectoryExists( user.ProfilePath + "Desktop" ) )
			vfs.CreateDirectory( user.ProfilePath + "Desktop" );
		if ( !vfs.DirectoryExists( user.ProfilePath + "My Documents" ) )
			vfs.CreateDirectory( user.ProfilePath + "My Documents" );
		if ( !vfs.DirectoryExists( user.ProfilePath + "Start Menu" ) )
			vfs.CreateDirectory( user.ProfilePath + "Start Menu" );
	}
}
