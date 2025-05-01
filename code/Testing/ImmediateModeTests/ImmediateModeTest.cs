using Sandbox;
using XGUI.ImmediateMode;

public sealed class ImmediateModeTest : Component
{
	private bool showWindow = true;
	private bool checkboxValue = false;
	private int intValue = 0;
	private float floatValue = 0.5f;
	private string inputText = "Hello";
	private int clickCount = 0;
	protected override void OnUpdate()
	{
		// Create a window
		if ( ImXGUI.Begin( "ImXGUI Window", ref showWindow ) )
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

			ImXGUI.InputInt( "Int Input", ref intValue );
			ImXGUI.InputFloat( "Float Input", ref floatValue );

			ImXGUI.SliderInt( "Int Slider", ref intValue, -1, 3 );
			ImXGUI.SliderFloat( "Float Slider", ref floatValue, 0.0f, 1.0f, 0.025f );

			if ( ImXGUI.InputText( "Enter Text", ref inputText ) )
			{
				Log.Info( $"Input changed to: {inputText}" );
			}

			ImXGUI.End();
		}
	}
}
