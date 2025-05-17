using FakeOperatingSystem;

public class EditProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/System32/edit.exe";
	public override bool ConsoleApp => true;

	/*
	   File  Edit  Search  View  Options  Help                                     
	┌───────────────────────────────── UNTITLED1 ─────────────────────────────────┐
	│                                                                             ↑
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             █
	│                                                                             ↓
	 F1=Help                                              │ Line:1    Col:1        
	*/

	public override void Main( NativeProcess process, Win32LaunchOptions launchOptions = null )
	{
		StandardOutput.Write( "blah" );
		StandardInput.Read();
	}
}
