// code/FakeOperatingSystem/Process/BaseProcess.cs
namespace FakeOperatingSystem;
public abstract class BaseProcess
{
	public int ProcessId { get; internal set; }
	public string ProcessName { get; protected set; }
	public string ProcessFilePath { get; protected set; }
	public Win32LaunchOptions LaunchOptions { get; protected set; }
	public ProcessManager Manager { get; internal set; }
	public abstract void Start();
	public abstract void Terminate();
}
