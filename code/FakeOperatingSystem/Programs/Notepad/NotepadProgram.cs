// code/FakeOperatingSystem/Programs/Notepad/NotepadProgram.cs
using FakeDesktop;

namespace FakeOperatingSystem;
public class NotepadProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/Notepad.exe";
	public override void Main( NativeProcess process )
	{
		var window = new Notepad(); // Your Notepad.razor window
		process.RegisterWindow( window ); ;
	}
}
