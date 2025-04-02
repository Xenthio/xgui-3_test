using Sandbox;
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
			var a = new MenuTest();
			XGUIRootPanel.Current.AddChild( a );
			var b = new About();
			XGUIRootPanel.Current.AddChild( b );
			var c = new OptionsThemable();
			XGUIRootPanel.Current.AddChild( c );
			var d = new GameLauncher();
			XGUIRootPanel.Current.AddChild( d );
			var e = new PlayGamesWindow();
			XGUIRootPanel.Current.AddChild( e );
			hi = 10;
		}
		else if ( hi < 3 )
		{
			hi++;
		}
	}
}
