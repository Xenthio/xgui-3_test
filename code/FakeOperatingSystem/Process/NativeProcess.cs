// code/FakeOperatingSystem/Process/NativeProcess.cs
using System.Collections.Generic;
using XGUI;

namespace FakeOperatingSystem;
public class NativeProcess : BaseProcess
{
	public List<Window> OwnedWindows { get; } = new();
	public NativeProgram Program { get; }

	public NativeProcess( NativeProgram program, Win32LaunchOptions options )
	{
		Program = program;
		LaunchOptions = options;
		ProcessName = program.GetType().Name;
		ProcessFilePath = program.FilePath;
	}

	public override void Start()
	{
		// Let the program create its windows and perform startup logic
		Program.Main( this );
		// Optionally assign a unique process ID
		ProcessId = GetHashCode();
	}

	public override void Terminate()
	{
		// Close all owned windows (if not already closed by the program)
		foreach ( var window in OwnedWindows.ToArray() )
		{
			window?.Close();
		}
		OwnedWindows.Clear();
	}

	public void RegisterWindow( Window window )
	{
		if ( window != null && !OwnedWindows.Contains( window ) )
		{
			OwnedWindows.Add( window );
			XGUISystem.Instance.Panel.AddChild( window );
			window.OnCloseAction += () =>
			{
				OwnedWindows.Remove( window );
				if ( OwnedWindows.Count == 0 )
				{
					Terminate();
				}
			};
		}
	}
}
