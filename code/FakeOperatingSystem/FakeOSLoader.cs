using FakeDesktop;
using FakeOperatingSystem.OSFileSystem;
using FakeOperatingSystem.Shell;
using Sandbox;
using XGUI;

namespace FakeOperatingSystem;

public class FakeOSLoader : Component
{
	OldVirtualFileSystem _oldVirtualFileSystem;
	public VirtualFileSystem VirtualFileSystem;
	public ShellNamespace ShellNamespace;
	ProcessManager _processManager;
	protected override void OnStart()
	{
		XGUISystem.Instance.SetGlobalTheme( "/XGUI/DefaultStyles/Computer95.scss" );

		// Initialize the virtual file system
		_oldVirtualFileSystem = new OldVirtualFileSystem( FileSystem.Data, "FakeSystemRoot" );
		VirtualFileSystem = new VirtualFileSystem( FileSystem.Data );


		ShellNamespace = new ShellNamespace( VirtualFileSystem );

		FileAssociationManager.Initialize( VirtualFileSystem );

		// initialize program manager
		_processManager = new ProcessManager();

		ThemeResources.ReloadAll();

		//Scene.GetSystem<XGUISystem>().Panel.AddChild<Explorer>();
		_processManager.OpenExecutable( "C:/Windows/explorer.exe", new Win32LaunchOptions() );
		Scene.GetSystem<XGUISystem>().Panel.AddChild<TaskBar>();
		Scene.GetSystem<XGUISystem>().Panel.AddChild<Desktop>();
		base.OnStart();
	}
}
