using Sandbox;
using XGUI.ImmediateMode;

public sealed class ImmediateModeTest : Component
{
	private bool showWindow = true;
	private bool checkboxValue = false;
	private float sliderValue = 0.5f;
	private string inputText = "Hello";
	private int clickCount = 0;
	protected override void OnUpdate()
	{
		// Create a window
		if ( ImXGUI.Begin( "My Window", ref showWindow ) )
		{
			ImXGUI.Text( "Welcome to ImXGUI!" );

			if ( ImXGUI.Button( $"Click Me! ({clickCount} Clicks)" ) )
			{
				Log.Info( "Button clicked!" );
				clickCount++;
			}

			if ( ImXGUI.Checkbox( "Toggle Option", ref checkboxValue ) )
			{
				Log.Info( $"Checkbox changed to: {checkboxValue}" );
			}

			ImXGUI.Slider( "Adjust Value", ref sliderValue, 0, 100 );

			if ( ImXGUI.InputText( "Enter Text", ref inputText ) )
			{
				Log.Info( $"Input changed to: {inputText}" );
			}

			ImXGUI.End();
		}
	}
}
