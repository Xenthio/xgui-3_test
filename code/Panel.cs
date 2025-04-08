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
		if ( hi == 3 && Scene.GetSystem<XGUISystem>().Panel != null )
		{
			Log.Info( "adding XGUI Panel" );
			Scene.GetSystem<XGUISystem>().Panel.AddChild<panelselector>();
			Scene.GetSystem<XGUISystem>().Panel.AddChild<OptionsThemable>();
			Scene.GetSystem<XGUISystem>().Panel.AddChild<ImmediateTheme>();
			Scene.GetSystem<XGUISystem>().Panel.AddChild<MenuTest>();
			Scene.GetSystem<XGUISystem>().Panel.AddChild<About>();
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
		Game.ActiveScene.GetSystem<XGUISystem>().Panel.AddChild( a );
		Game.ActiveScene.GetSystem<XGUISystem>().Panel.SetChildIndex( a, 0 );
	}
}
