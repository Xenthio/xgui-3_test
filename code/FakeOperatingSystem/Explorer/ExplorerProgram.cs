using FakeOperatingSystem;

public class ExplorerProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/explorer.exe";
	public override void Main( NativeProcess process )
	{
		// TODO: Replace with your actual Explorer window/panel
		var window = new Explorer();
		process.RegisterWindow( window );
	}
}
