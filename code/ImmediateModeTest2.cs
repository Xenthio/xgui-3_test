using Sandbox;
using XGUI.ImmediateMode;

public sealed class ImmediateModeTest2 : Component
{
	private bool showWindow = true;
	private Color colour = Color.White;
	[Property] public ModelRenderer Model { get; set; }
	protected override void OnUpdate()
	{
		// Create a window
		if ( ImXGUI.Begin( "My EVIL Window", ref showWindow ) )
		{
			ImXGUI.Text( "Model Colour." );

			ImXGUI.ColorPicker( "Colour", ref colour );

			Model.Tint = colour;
		}
	}
}
