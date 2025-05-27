using FakeOperatingSystem.Shell;

public class DesktopSettingsApplet : IControlPanelApplet
{
	public string Name => "Display";
	public string IconName => "display";
	public string Description => "Change screen resolution and appearance.";

	public void Launch()
	{
		// Open DisplaySettings.razor window
	}
}
