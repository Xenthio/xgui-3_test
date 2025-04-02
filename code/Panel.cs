using Sandbox;
using Sandbox.UI;
using XGUI;

public sealed class OpenMenu : Component
{
	protected override void OnEnabled()
	{
		base.OnEnabled();
	}
	int hi = 0;
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( hi == 3 && XGUIRootPanel.Current != null )
		{
			Log.Info( "adding XGUI Panel" );
			var a = new panelselector();
			XGUIRootPanel.Current.AddChild( a );
			var b = new OptionsThemable();
			XGUIRootPanel.Current.AddChild( b );
			hi = 10;
		}
		else if ( hi < 3 )
		{
			hi++;
		}
	}

	[ConCmd]
	public static void openpanel( string panel )
	{
		var a = TypeLibrary.GetType( panel ).Create<Panel>();
		XGUIRootPanel.Current.AddChild( a );
		XGUIRootPanel.Current.SetChildIndex( a, 0 );
	}
}
