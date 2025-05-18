using FakeOperatingSystem.Console;
using Sandbox.UI;
using System.IO;
using XGUI;

[StyleSheet]
internal class ConsoleHost : Window
{

	int consoleWidth = 80 * 9; // Default console width
	int consoleHeight = 25 * 16; // Default console height

	private ConsolePanel ActiveConsolePanel;

	private ConsoleHostWriter writer;
	private ConsoleHostReader reader;

	private Panel WindowContent;
	private LayoutBoxInset ConsoleBox;

	public ConsoleHost()
	{
		SetClass( "console-window", true );
		reader = new ConsoleHostReader();
		writer = new ConsoleHostWriter( AppendOutput );/*
		Size.x = consoleWidth;
		Size.y = consoleHeight;*/

		WindowContent = Add.Panel( "window-content" );
		ConsoleBox = WindowContent.AddChild<LayoutBoxInset>();
		ConsoleBox.AddClass( "console-box" );

		ActiveConsolePanel = ConsoleBox.AddChild<ConsolePanel>();
		ActiveConsolePanel.Initialize( writer, reader, SetWindowTitle );

		Title = "Command Prompt";
	}

	bool initialised = false;
	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );
		if ( !initialised && ActiveConsolePanel != null )
		{// Set window width and height so that ActiveConsolePanel is the console size 
			float currentWindowWidth = Box.Rect.Width;
			float currentWindowHeight = Box.Rect.Height;

			float currentConsolePanelWidth = ActiveConsolePanel.Box.Rect.Width;
			float currentConsolePanelHeight = ActiveConsolePanel.Box.Rect.Height;

			float chromeWidth = currentWindowWidth - currentConsolePanelWidth;
			float chromeHeight = currentWindowHeight - currentConsolePanelHeight;

			Size = new Vector2( consoleWidth + chromeWidth, consoleHeight + chromeHeight );

			Style.Width = Size.x;
			Style.Height = Size.y;
			// Optional: Set MinSize if the window should not be resizable below this calculated size
			// MinSize = Size; 
			Log.Info( "ConsoleHost: Initializing ConsolePanel" );
			initialised = true;
		}
	}

	private void AppendOutput( char c )
	{
		ActiveConsolePanel.AppendOutput( c );
	}

	private void SetWindowTitle( string newTitle )
	{
		Title = newTitle;
	}

	public TextWriter GetOutputWriter() => writer; // Return the instance created in ConsoleHost
	public TextReader GetInputReader() => reader; // Return the instance created in ConsoleHost

	public override void Tick()
	{
		base.Tick();
		if ( ActiveConsolePanel != null )
		{
			ActiveConsolePanel.AcceptsFocus = true;
		}
	}

	public override bool HasContent => true;

	protected override int BuildHash()
	{
		return System.HashCode.Combine( Title, ActiveConsolePanel, writer, reader );
	}
}
