using Sandbox;
using XGUI.ImmediateMode;

public sealed class ImmediateModeTest3 : Component
{
	private bool showWindow = true;
	private string testString = "";
	protected override void OnFixedUpdate()
	{
		// Create a window
		if ( ImXGUI.Begin( "My EVILer Window", ref showWindow ) )
		{
			ImXGUI.Text( "Hello World!" );
			ImXGUI.Text( "Hello World!" );
			ImXGUI.Text( "Hello World!" );
			ImXGUI.Text( "Hello World!" );

			ImXGUI.Button( "Hello" );
			ImXGUI.Button( "I am another button." );
			ImXGUI.Button( "I too am in this episode." );
			ImXGUI.Button( "Big ass button." );
			ImXGUI.Text( "More text blah blah blah" );

			if ( ImXGUI.Button( $"Click me for a surprise" ) )
			{
				testString += "Hello! ";
			}
			ImXGUI.Text( testString );
		}
	}
}
