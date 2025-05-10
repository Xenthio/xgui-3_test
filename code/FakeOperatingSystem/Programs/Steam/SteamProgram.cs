using FakeOperatingSystem;

public class SteamProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Program Files/Steam/steam.exe";
	public override void Main( NativeProcess process )
	{
		// TODO: Replace with your actual Steam window/panel
		var window = new GameLauncher();
		process.RegisterWindow( window );
		// window.Show();
	}
}
