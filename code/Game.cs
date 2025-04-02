using Sandbox.UI;
using XGUI;

//
// You don't need to put things in a namespace, but it doesn't hurt.
//
namespace Sandbox;

/// <summary>
/// This is your game class. This is an entity that is created serverside when
/// the game starts, and is replicated to the client. 
/// 
/// You can use this to create things like HUDs and declare which player class
/// to use for spawned players.
/// </summary>
public partial class XGUITEST : Component
{
	[ConCmd]
	public static void openpanel( string panel )
	{
		var a = TypeLibrary.GetType( panel ).Create<Panel>();
		XGUIRootPanel.Current.AddChild( a );
		XGUIRootPanel.Current.SetChildIndex( a, 0 );
	}
}
