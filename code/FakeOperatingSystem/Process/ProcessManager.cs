// code/FakeOperatingSystem/Process/ProcessManager.cs
using System.Collections.Generic;

namespace FakeOperatingSystem;
public class ProcessManager
{
	public static ProcessManager Instance { get; private set; }
	private List<BaseProcess> _processes = new();
	private static int _lastProcessId = 0;
	public ProcessManager()
	{
		Instance?.TerminateAll();
		Instance = this;
	}
	public void RegisterProcess( BaseProcess process )
	{
		if ( !_processes.Contains( process ) )
		{
			process.Manager = this;
			_processes.Add( process );
			process.ProcessId = ++_lastProcessId;
		}
	}
	public void TerminateProcess( BaseProcess process )
	{
		if ( _processes.Contains( process ) )
		{
			process.Terminate();
			_processes.Remove( process );
		}
	}
	public void TerminateAll()
	{
		foreach ( var process in _processes.ToArray() )
		{
			process.Terminate();
		}
		_processes.Clear();
	}
	public IEnumerable<BaseProcess> GetProcesses() => _processes;
	public int GetProcessCount() => _processes.Count;

	/// <summary>
	/// Opens an executable, deciding if it's a NativeProcess or X86PEProcess.
	/// </summary>
	public BaseProcess OpenExecutable( string exePath, Win32LaunchOptions options )
	{
		// Try to load as a NativeProgram (fake exe)
		var nativeProgram = NativeProgram.ReadFromExe( exePath );
		BaseProcess process;
		if ( nativeProgram != null )
		{
			process = new NativeProcess( nativeProgram, options );
		}
		else
		{
			// Fallback: treat as real PE
			process = new X86PEProcess( exePath, options );
		}
		RegisterProcess( process );
		process.Start();
		return process;
	}
}
