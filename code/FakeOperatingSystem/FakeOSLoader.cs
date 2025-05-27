using FakeDesktop;
using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.Shell;
using Sandbox;
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

		// Initialize the virtual file system
		VirtualFileSystem = new VirtualFileSystem( FileSystem.Data );

		Boot();
		base.OnStart();
	}

	/// <summary>
	/// This would be boot.
	/// </summary>
	public void Boot()
	{

		Instance = this;

		// Initialize the old virtual file system (for compatibility with old code, this is more like shell namespace)
		_oldVirtualFileSystem = new OldVirtualFileSystem( FileSystem.Data, "FakeSystemRoot" );

		// Do core OS file setup (no file copying setup process or ui for now)
		FakeSystemRoot.TryCreateSystemRoot();

		// Initialize the registry (add this)
		Registry = new Registry();

		// initialize program manager
		_processManager = new ProcessManager();

		ShellNamespace = new ShellNamespace( VirtualFileSystem );

		FileAssociationManager.Initialize( VirtualFileSystem );

		// Todo: Move to Logon
		ThemeResources.ReloadAll();

		_processManager.OpenExecutable( "C:/Windows/explorer.exe", new Win32LaunchOptions() );

		// Move TaskBar and Desktop to Explorer.
		Scene.GetSystem<XGUISystem>().Panel.AddChild<TaskBar>();
		Scene.GetSystem<XGUISystem>().Panel.AddChild<Desktop>();
	}
}
