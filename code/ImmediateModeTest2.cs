using Sandbox;
using XGUI.ImmediateMode;

public sealed class ImmediateModeTest2 : Component
{
	private bool showWindow = true;
	private Color colour = Color.White;
	private int clickCount = 0;
	private bool checkboxValue = false;
	private int intValue = 0;
	private float floatValue = 0.5f;
	[Property] public ModelRenderer Model { get; set; }
	protected override void OnFixedUpdate()
	{
		// Create a window
		if ( ImXGUI.Begin( "My EVIL Window", ref showWindow ) )
		{
			ImXGUI.Text( "Model Colour." );

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

			ImXGUI.ColorPicker( "Colour", ref colour );

			Model.Tint = colour;
		}
	}
}
