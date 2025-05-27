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

	public static FakeOSLoader Instance;
	OldVirtualFileSystem _oldVirtualFileSystem;
	public VirtualFileSystem VirtualFileSystem;
	public ShellNamespace ShellNamespace;
	ProcessManager _processManager;
	public Registry Registry;


	/// <summary>
	/// This would be boot.
	/// </summary>
	protected override void OnStart()
	{
		Instance = this;
		XGUISystem.Instance.SetGlobalTheme( "/XGUI/DefaultStyles/Computer95.scss" );

		// Initialize the virtual file system
		_oldVirtualFileSystem = new OldVirtualFileSystem( FileSystem.Data, "FakeSystemRoot" );
		VirtualFileSystem = new VirtualFileSystem( FileSystem.Data );

		// Initialize the registry (add this)
		Registry = new Registry();

		ShellNamespace = new ShellNamespace( VirtualFileSystem );

		FileAssociationManager.Initialize( VirtualFileSystem );

		// initialize program manager
		_processManager = new ProcessManager();

		ThemeResources.ReloadAll();

		_processManager.OpenExecutable( "C:/Windows/explorer.exe", new Win32LaunchOptions() );
		Scene.GetSystem<XGUISystem>().Panel.AddChild<TaskBar>();
		Scene.GetSystem<XGUISystem>().Panel.AddChild<Desktop>();
		base.OnStart();
	}
}
