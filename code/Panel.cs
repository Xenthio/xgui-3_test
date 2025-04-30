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
			//Scene.GetSystem<XGUISystem>().Panel.AddChild<OptionsThemable>();
			Scene.GetSystem<XGUISystem>().Panel.AddChild<TaskBar>();
			//Scene.GetSystem<XGUISystem>().Panel.AddChild<MenuTest>();
			Scene.GetSystem<XGUISystem>().Panel.AddChild<About>();
			//Scene.GetSystem<XGUISystem>().Panel.AddChild<AboutNew>();
			Scene.GetSystem<XGUISystem>().Panel.AddChild<GlobalStyle>();
			hi = 10;
		}
		else if ( hi < 3 )
		{
			hi++;
		}
		if ( Scene.GetSystem<XGUISystem>() is XGUISystem xgui )
		{
			if ( Input.Pressed( "Score" ) )
			{
				xgui.Component.MouseUnlocked = !xgui.Component.MouseUnlocked;
			}
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
