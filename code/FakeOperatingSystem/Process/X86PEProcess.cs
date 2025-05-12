// code/FakeOperatingSystem/Process/X86PEProcess.cs
using FakeDesktop;
using FakeOperatingSystem.Experiments.Ambitious.X86;
using System.IO;
using System.Threading.Tasks;

namespace FakeOperatingSystem;
public class X86PEProcess : BaseProcess
{
	private X86Interpreter _interpreter;
	private Task _executionTask;

	public X86PEProcess( string exePath, Win32LaunchOptions options )
	{
		LaunchOptions = options;
		ProcessName = Path.GetFileNameWithoutExtension( exePath );
		ProcessFileName = Path.GetFileName( exePath );
		ProcessFilePath = exePath;
		_interpreter = new X86Interpreter();
	}

	public override void Start()
	{
		// lookup in virtual file system 
		if ( !VirtualFileSystem.Instance.PathExists( ProcessFilePath ) )
		{
			Log.Warning( $"Executable not found: {ProcessFilePath}" );
			Manager.TerminateProcess( this );
			return;
		}
		var file = VirtualFileSystem.Instance.GetEntry( ProcessFilePath );

		byte[] fileBytes = file.AssociatedFileSystem.ReadAllBytes( file.RealPath ).ToArray();
		if ( !_interpreter.LoadExecutable( fileBytes, ProcessFilePath ) )
		{
			Log.Warning( $"Failed to load PE executable: {ProcessFilePath}" );
			return;
		}

		// Optionally, set up interpreter event hooks here (e.g., for message boxes)
		_interpreter.OnHaltWithMessageBox += ( title, message, icon, buttons ) =>
		{
			MessageBoxUtility.ShowCustom( message, title, icon, buttons );
		};

		// Start execution asynchronously

		_interpreter.OnFinish += () =>
		{
			Manager.TerminateProcess( this );
		};
		_executionTask = _interpreter.ExecuteAsync();

		// Optionally, you can log or track the task for process management
	}

	public override void Terminate()
	{
		_interpreter.Halt();
	}
}
