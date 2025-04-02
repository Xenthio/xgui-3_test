using Sandbox;
using XGUI.ImmediateMode;

public sealed class ImmediateModeTest2 : Component
{
	private bool showWindow = true;
	protected override void OnUpdate()
	{
		// Create a window
		if ( ImXGUI.Begin( "My EVIL Window", ref showWindow ) )
		{
			ImXGUI.Text( "Welcome to EVIL IMXGUI!" );

			if ( ImXGUI.Button( "EVIL BUTTON" ) )
			{
				Log.Info( "MWUHAHAHAHAHA!" );
			}

			ImXGUI.Button( "EVIL BUTTON 2" );
			ImXGUI.Button( "EVIL BUTTON 3" );

			ImXGUI.End();
		}
	}
}
