using FakeOperatingSystem;

public class PaintProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/mspaint.exe";
	public override void Main( NativeProcess process )
	{
		// TODO: Replace with your actual Paint window/panel
		// var window = new Paint();
		// process.RegisterWindow(window);
		// window.Show();
	}
}
