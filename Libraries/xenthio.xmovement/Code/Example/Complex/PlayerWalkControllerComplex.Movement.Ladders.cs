using Sandbox;
using System;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, FeatureEnabled( "Ladders" )] public bool EnableLadders { get; set; } = true;
	[Property, Feature( "Ladders" )] public float ClimbSpeed { get; set; } = 100.0f;
	[Property, Feature( "Ladders" )] public string LadderTag { get; set; } = "ladder";
	bool IsTouchingLadder = false;
	Vector3 LadderNormal;

	Vector3 LastNonZeroWishLadderVelocity;
	public virtual void CheckLadder()
	{
		if ( !EnableLadders ) { IsTouchingLadder = false; return; }
		var wishvel = new Vector3( WishMove.x.Clamp( -1f, 1f ), WishMove.y.Clamp( -1f, 1f ), 0 );

		// this is for latching
		if ( wishvel.Length > 0 ) LastNonZeroWishLadderVelocity = wishvel;
		if ( TryLatchNextTickCounter > 0 ) wishvel = LastNonZeroWishLadderVelocity * -1;

		wishvel *= EyeAngles.WithPitch( 0 ).ToRotation();
		wishvel = wishvel.Normal;

		if ( IsTouchingLadder )
		{
			if ( Input.Pressed( "jump" ) )
			{
				var sidem = (Math.Abs( Head.WorldRotation.Forward.Abs().z - 1 ) * 3).Clamp( 0, 1 );
				var upm = Head.WorldRotation.Forward.z;

				var Eject = new Vector3();

				Eject.x = LadderNormal.x * sidem;
				Eject.y = LadderNormal.y * sidem;
				Eject.z = (3 * upm).Clamp( 0, 1 );

				Controller.Velocity += (Eject * 180.0f) * WorldScale;

				IsTouchingLadder = false;

				return;

			}
			else if ( Controller.GroundObject != null && LadderNormal.Dot( wishvel ) > 0 )
			{
				IsTouchingLadder = false;

				return;
			}
		}

		const float ladderDistance = 1.0f;
		var start = WorldPosition;
		Vector3 end = start + (IsTouchingLadder ? (LadderNormal * -1.0f) : wishvel) * ladderDistance;

		var pm = Scene.Trace.Ray( start, end )
					.Size( Controller.BoundingBox.Mins, Controller.BoundingBox.Maxs )
					.WithTag( LadderTag )
					.IgnoreGameObjectHierarchy( GameObject )
					.Run();

		// Gizmo.Draw.LineBBox( cc.BoundingBox.Translate( end ) );

		IsTouchingLadder = false;

		if ( pm.Hit )
		{
			IsTouchingLadder = true;
			LadderNormal = pm.Normal;
		}
	}

	public virtual void LadderMove()
	{
		Controller.IsOnGround = false;

		var velocity = Controller.WishVelocity;
		float normalDot = velocity.Dot( LadderNormal );
		var cross = LadderNormal * normalDot;
		Controller.Velocity = (velocity - cross) + (-normalDot * LadderNormal.Cross( Vector3.Up.Cross( LadderNormal ).Normal ));

		Controller.Move( withWishVelocity: false, withGravity: false );
	}

	[ConVar( "debug_movement_ladderlatching" )]
	public static bool LatchDebug { get; set; } = false;

	int TryLatchNextTickCounter = 0;
	Vector3 LastNonZeroWishVelocity;
	private void LadderLatchCheck()
	{
		if ( !EnableLadders ) return;

		if ( Input.Down( JumpAction ) || Input.Released( JumpAction ) )
		{
			TryLatchNextTickCounter = 0;
			return;
		}

		if ( !Controller.WishVelocity.Normal.IsNearlyZero( 0.001f ) )
		{
			LastNonZeroWishVelocity = Controller.WishVelocity;
		}

		if ( TryLatchNextTickCounter > 0 )
		{
			Controller.Velocity = (LastNonZeroWishVelocity.Normal * -100).WithZ( Controller.Velocity.z );
			TryLatchNextTickCounter--;
		}

		if ( Controller.GroundObject != null ) return;
		if ( Controller.PreviousGroundObject == null ) return;

		// Trace downwards and behind the way we are or was walking

		var start = WorldPosition + (Vector3.Down * 16);
		var end = start - (LastNonZeroWishVelocity.Normal * (Controller.Radius / 2));

		var pm = Scene.Trace.Ray( start, end )
					.Size( Controller.BoundingBox.Mins, Controller.BoundingBox.Maxs )
					.WithTag( LadderTag )
					.IgnoreGameObjectHierarchy( GameObject )
					.Run();

		if ( pm.Hit )
		{
			Controller.Velocity = Vector3.Zero.WithZ( Controller.Velocity.z );
			TryLatchNextTickCounter = 10;
		}

		if ( LatchDebug )
		{
			DebugOverlay.Line( WorldPosition, start, Color.Yellow, 10 );
			DebugOverlay.Line( pm.StartPosition, pm.EndPosition, Color.Red, 10 );
		}
	}
}
