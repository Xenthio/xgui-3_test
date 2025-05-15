// code/FakeOperatingSystem/Process/BaseProcess.cs
using System;
using System.Collections.Generic;
using XGUI;

namespace FakeOperatingSystem;

public enum ProcessStatus
{
	Running,
	Suspended,
	Terminated
}

public abstract class BaseProcess
{
	public int ProcessId { get; internal set; }
	public string ProcessName { get; protected set; }
	public string ProcessFileName { get; protected set; }
	public string ProcessFilePath { get; protected set; }
	public Win32LaunchOptions LaunchOptions { get; protected set; }
	public ProcessManager Manager { get; internal set; }
	public ProcessStatus Status { get; set; } = ProcessStatus.Running;
	public int? ParentProcessId { get; set; }
	public DateTime StartTime { get; set; } = DateTime.Now;
	public List<Window> OwnedWindows { get; } = new();
	public abstract void Start();
	public abstract void Terminate();
}
