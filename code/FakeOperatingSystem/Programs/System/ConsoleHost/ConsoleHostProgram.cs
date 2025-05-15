using FakeOperatingSystem;

public class ConsoleHostProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/System32/conhost.exe";
	BaseProcess MainChildProcess;
	public override void Main( NativeProcess process, Win32LaunchOptions launchOptions = null )
	{
		var consoleHost = new ConsoleHost();
		process.RegisterWindow( consoleHost );

		// If no arguments, launch cmd.exe as a child process
		if ( launchOptions == null || launchOptions.Arguments == null || launchOptions.Arguments.Length == 0 )
		{
			// Create launch options for cmd
			var cmdOptions = new Win32LaunchOptions
			{
				Arguments = "",
				ParentProcessId = process.ProcessId,
				StandardOutputOverride = consoleHost.GetOutputWriter(),
				StandardInputOverride = consoleHost.GetInputReader(),
			};

			MainChildProcess = ProcessManager.Instance.OpenExecutable( "C:/Windows/System32/cmd.exe", cmdOptions );
		}
		// else: handle launching other programs as needed
	}

	public void OnClose()
	{
		ProcessManager.Instance.TerminateProcess( MainChildProcess );
	}
}
