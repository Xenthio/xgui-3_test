using FakeDesktop;
using Sandbox;
using XGUI;

namespace FakeOperatingSystem;

public class FakeOSLoader : Component
{
	VirtualFileSystem _virtualFileSystem;
	ProcessManager _processManager;
	protected override void OnStart()
	{
		XGUISystem.Instance.SetGlobalTheme( "/XGUI/DefaultStyles/Computer95.scss" );

		// Initialize the virtual file system
		_virtualFileSystem = new VirtualFileSystem( FileSystem.Data, "FakeSystemRoot" );

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
