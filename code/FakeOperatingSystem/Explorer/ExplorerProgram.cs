using FakeOperatingSystem;

public class ExplorerProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/explorer.exe";
	public override void Main( NativeProcess process, Win32LaunchOptions launchOptions = null )
	{
		// TODO: Replace with your actual Explorer window/panel
		var window = new Explorer();
		if ( launchOptions != null )
		{
			window.InitialPath = launchOptions.Arguments;
		}
		process.RegisterWindow( window );
	}
}
