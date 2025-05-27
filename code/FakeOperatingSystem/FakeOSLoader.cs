using FakeDesktop;
using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.Shell;
using Sandbox;
using System.Linq;
using System.Threading.Tasks;
using XGUI;

namespace FakeOperatingSystem;

public class FakeOSLoader : Component
{
	public static int VersionNumber = 0;
	public static string VersionString = "XGUI-3 FakeOS v0.0 (no versioning)";

	public static bool UserSystemEnabled = true; // Set to false for single-user mode

	public static FakeOSLoader Instance;
	OldVirtualFileSystem _oldVirtualFileSystem;
	public VirtualFileSystem VirtualFileSystem;
	public ShellNamespace ShellNamespace;
	ProcessManager _processManager;
	public Registry Registry;
	public UserManager UserManager;


	protected override void OnStart()
	{
		XGUISystem.Instance.SetGlobalTheme( "/XGUI/DefaultStyles/Computer95.scss" );

		// Do core OS file setup (no file copying setup process or ui for now)
		FakeSystemRoot.TryCreateSystemRoot();

		// Initialize the virtual file system
		VirtualFileSystem = new VirtualFileSystem( FileSystem.Data );

		Boot();
		base.OnStart();
	}

	/// <summary>
	/// This would be boot.
	/// </summary>
	public async Task Boot() // Changed to async Task
	{
		Instance = this;
		_oldVirtualFileSystem = new OldVirtualFileSystem( FileSystem.Data, "FakeSystemRoot" );
		Registry = new Registry();
		UserManager = new UserManager();
		UserManager.LoadUsers(); // Load existing users first

		// Check if user system is enabled in registry
		// Request a nullable int (int?). If the value doesn't exist, GetValue will return default(int?), which is null.
		int? userProfilesSetting = Registry.GetValue<int?>( @"HKEY_LOCAL_MACHINE\Network\Logon", "UserProfiles", null );

		if ( userProfilesSetting == null ) // Now this correctly checks if the value was not found or was explicitly null
		{
			// First boot or registry value missing: show create user dialog
			ShowCreateUserDialog();
			return; // Wait for dialog result before continuing boot
		}
		else
		{
			// Value exists, proceed to use it
			UserSystemEnabled = userProfilesSetting.Value == 1;
		}

		// If user system is enabled, but no user is set (e.g. after first boot dialog was skipped then re-enabled manually)
		// or if it's just a normal boot with users enabled.
		if ( UserSystemEnabled )
		{
			if ( UserManager.Users.Any() )
			{
				// TODO: Implement a proper Logon Dialog here.
				// For now, auto-login the first user if one exists.
				var userToLogin = UserManager.Users.First();
				if ( UserManager.Login( userToLogin.UserName, userToLogin.PasswordHash ) ) // Assuming Login sets CurrentUser
				{
					Log.Info( $"Auto-logged in as: {UserManager.CurrentUser.UserName}" );
					// Load the user's hive
					Registry.LoadUserHive( UserManager.CurrentUser.UserName, UserManager.CurrentUser.RegistryHivePath );
					//await UserManager.SetupUserProfile( UserManager.CurrentUser ); // Await here
				}
				else
				{
					Log.Error( "Auto-login failed. Halting boot for user setup." );
					// Potentially show login dialog again or an error.
					return;
				}
			}
			else
			{
				Log.Warning( "User system enabled, but no users found. Showing create user dialog." );
				ShowCreateUserDialog();
				return;
			}
		}
		else
		{
			// Single-user mode: set up global folders
			//await UserManager.SetupUserProfile( null ); // Await here
			Registry.LoadUserHive( "Default", @"C:\Windows\USER.DAT" );
		}

		ContinueBoot();
	}

	private void ShowCreateUserDialog()
	{
		// Pseudocode for dialog
		var dialog = new CreateUserDialog(
			onCreate: async ( username, password ) => // Lambda becomes async
			{
				UserSystemEnabled = true;
				Registry.SetValue( @"HKEY_LOCAL_MACHINE\Network\Logon", "UserProfiles", 1 );
				var user = new UserAccount
				{
					UserName = username,
					PasswordHash = password, // Hash in real code!
					ProfilePath = $@"C:\Documents and Settings\{username}\",
					RegistryHivePath = $@"C:\Documents and Settings\{username}\NTUSER.DAT"
				};
				UserManager.Users.Add( user );
				UserManager.SaveUsers();
				// Create All Users folder
				var vfs = VirtualFileSystem.Instance;
				var allUsers = @"C:\Documents and Settings\All Users";
				if ( !vfs.DirectoryExists( allUsers ) )
					vfs.CreateDirectory( allUsers );
				await UserManager.SetupUserProfile( user ); // Await here
				UserManager.Login( username, password ); // This should ideally also set CurrentUser for ContinueBoot
				ContinueBoot(); // This might need to be awaited if it also becomes async
			},
			onSkip: async () => // Lambda becomes async
			{
				UserSystemEnabled = false;
				Registry.SetValue( @"HKEY_LOCAL_MACHINE\Network\Logon", "UserProfiles", 0 );
				await UserManager.SetupUserProfile( null ); // Await here
				ContinueBoot();
			}
		);
		// Show dialog in your UI system
		XGUISystem.Instance.Panel.AddChild( dialog );
	}

	private void ContinueBoot()
	{
		_processManager = new ProcessManager();
		ShellNamespace = new ShellNamespace( VirtualFileSystem );
		FileAssociationManager.Initialize( VirtualFileSystem );
		ThemeResources.ReloadAll();
		_processManager.OpenExecutable( "C:/Windows/explorer.exe", new Win32LaunchOptions() );
		Scene.GetSystem<XGUISystem>().Panel.AddChild<TaskBar>();
		Scene.GetSystem<XGUISystem>().Panel.AddChild<Desktop>();
	}
}
