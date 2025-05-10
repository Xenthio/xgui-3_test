using FakeOperatingSystem;

public class TaskMgrProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/System32/taskmgr.exe";
	public override void Main( NativeProcess process )
	{
		// TODO: Replace with your actual Steam window/panel
		var window = new TaskMgr();
		process.RegisterWindow( window );
		// window.Show();
	}
}
