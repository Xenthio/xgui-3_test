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
		// Initialize the virtual file system
		_virtualFileSystem = new VirtualFileSystem( FileSystem.Data, "FakeSystemRoot" );

		// initialize program manager
		_processManager = new ProcessManager();

		Scene.GetSystem<XGUISystem>().Panel.AddChild<Explorer>();
		Scene.GetSystem<XGUISystem>().Panel.AddChild<TaskBar>();
		Scene.GetSystem<XGUISystem>().Panel.AddChild<Desktop>();
		base.OnStart();
	}
}
