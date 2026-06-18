// code/FakeOperatingSystem/Process/ProcessManager.cs
using FakeOperatingSystem.Experiments.Ambitious.X86.Win32;
using FakeOperatingSystem.OSFileSystem;
using System.Collections.Generic;

namespace FakeOperatingSystem;
public class ProcessManager
{
	public static ProcessManager Instance { get; private set; }
	private List<BaseProcess> _processes = new();
	private int _lastProcessId = 0;
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
	/// Console-subsystem PEs without a stdio override are automatically hosted inside conhost.
	/// </summary>
	public BaseProcess OpenExecutable( string exePath, Win32LaunchOptions options, bool shellLaunch = false )
	{
		// Try to load as a NativeProgram (fake exe)
		var nativeProgram = NativeProgram.ReadFromExe( exePath );

		// --- Decide if this is a console-mode app that needs conhost ---
		bool needsConhost = false;
		if ( options.StandardOutputOverride == null && options.StandardInputOverride == null )
		{
			if ( nativeProgram != null && nativeProgram.ConsoleApp )
			{
				needsConhost = true;
			}
			else if ( nativeProgram == null )
			{
				// Real PE: peek the subsystem field to see if it's CUI (3)
				try
				{
					if ( VirtualFileSystem.Instance.FileExists( exePath ) )
					{
						byte[] peBytes = VirtualFileSystem.Instance.ReadAllBytes( exePath );
						ushort subsystem = PELoader.PeekSubsystem( peBytes );
						if ( subsystem == 3 ) // IMAGE_SUBSYSTEM_WINDOWS_CUI
							needsConhost = true;
					}
				}
				catch { /* ignore, treat as GUI */ }
			}
		}

		if ( needsConhost )
		{
			// Route through conhost: conhost will re-open this exe with its streams wired
			var conProcess = OpenExecutable( "C:/Windows/System32/conhost.exe", new Win32LaunchOptions
			{
				Arguments = $"\"{exePath}\" {options.Arguments}",
				ParentProcessId = options.ParentProcessId,
			} );
			return conProcess;
		}

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

		if ( options.StandardInputOverride != null )
		{
			process.StandardInput = options.StandardInputOverride;
		}
		if ( options.StandardOutputOverride != null )
		{
			process.StandardOutput = options.StandardOutputOverride;
		}
		process.Start();
		return process;
	}

	public IEnumerable<BaseProcess> GetChildProcesses( int parentId )
	{
		foreach ( var process in _processes )
		{
			if ( process.ParentProcessId == parentId )
				yield return process;
		}
	}

	public BaseProcess GetProcessById( int processId )
	{
		foreach ( var process in _processes )
		{
			if ( process.ProcessId == processId )
			{
				return process;
			}
		}
		return null;
	}
}
