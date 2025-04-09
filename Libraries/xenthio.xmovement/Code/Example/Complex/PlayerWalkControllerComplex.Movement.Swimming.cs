﻿using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, FeatureEnabled( "Swimming" )] public bool EnableSwimming { get; set; } = true;
	[Property, Feature( "Swimming" )] public string WaterTag { get; set; } = "water";
	[Property, Feature( "Swimming" )] public float SwimmingSpeedScale { get; set; } = 0.8f;
	[Property, Feature( "Swimming" )] public float SwimmingFriction { get; set; } = 1;
	[Property, InputAction, Feature( "Swimming" )] public string SwimUpAction { get; set; } = "Jump";
	[Property, InputAction, Feature( "Swimming" )] public string SwimDownAction { get; set; } = "";
	private bool IsSwimming => WaterLevel > 0.5f;
	float WaterLevel = 0;
	public virtual void CheckWater()
	{
		if ( !EnableSwimming ) { WaterLevel = 0; return; }

		var start = WorldPosition + Controller.BoundingBox.Maxs.z;
		var end = WorldPosition;

		var pm = Scene.Trace.Ray( start, end )
					.Size( Controller.BoundingBox.Mins, Controller.BoundingBox.Maxs )
					.WithTag( WaterTag )
					.HitTriggers()
					.IgnoreGameObjectHierarchy( GameObject )
					.Run();
		WaterLevel = 1 - pm.Fraction;

		if ( WaterLevel > 0.1f )
		{
			if ( WaterLevel > 0.4f )
			{
				CheckWaterJump();
			}
			// If we are falling again, then we must not trying to jump out of water any more.
			if ( (Controller.Velocity.z < 0.0f) && IsJumpingFromWater )
			{
				WaterJumpTime = 0.0f;
			}
		}
	}

	public virtual void SwimMove()
	{
		Controller.IsOnGround = false;
		var wishvel = WishMove * EyeAngles.ToRotation();
		wishvel *= (GetWishSpeed() * SwimmingSpeedScale);
		if ( Input.Down( SwimUpAction ) ) wishvel = wishvel.WithZ( 100 );
		if ( Input.Down( SwimDownAction ) ) wishvel = wishvel.WithZ( -100 );
		Controller.WishVelocity = wishvel;
		Controller.Acceleration = Controller.BaseAcceleration;
		Controller.Accelerate( Controller.WishVelocity.ClampLength( 100 ) );

		Controller.Move( withWishVelocity: false, withGravity: false, frictionOverride: 1 );
	}

	protected float WaterJumpTime { get; set; }
	protected Vector3 WaterJumpVelocity { get; set; }
	protected bool IsJumpingFromWater => WaterJumpTime > 0;
	public virtual float WaterJumpHeight => 8;
	protected void CheckWaterJump()
	{
		// Already water jumping.
		if ( IsJumpingFromWater )
			return;

		// Don't hop out if we just jumped in
		// only hop out if we are moving up
		if ( Controller.Velocity.z < -180 )
			return;

		// See if we are backing up
		var flatvelocity = Controller.Velocity.WithZ( 0 );

		// Must be moving
		var curspeed = flatvelocity.Length;
		flatvelocity = flatvelocity.Normal;

		// see if near an edge
		var flatforward = Head.WorldRotation.Forward.WithZ( 0 ).Normal;

		// Are we backing into water from steps or something?  If so, don't pop forward
		if ( curspeed != 0 && Vector3.Dot( flatvelocity, flatforward ) < 0 )
			return;

		var vecStart = WorldPosition + (Controller.BoundingBox.Mins + Controller.BoundingBox.Maxs) * .5f;
		var vecEnd = vecStart + flatforward * 24;

		var tr = Controller.BuildTrace( vecStart, vecEnd ).Run();
		if ( tr.Fraction == 1 )
			return;

		vecStart.z = WorldPosition.z + HeadHeight + WaterJumpHeight;
		vecEnd = vecStart + flatforward * 24;
		WaterJumpVelocity = tr.Normal * -50;

		tr = Controller.BuildTrace( vecStart, vecEnd ).Run();
		if ( tr.Fraction < 1.0 )
			return;

		// Now trace down to see if we would actually land on a standable surface.
		vecStart = vecEnd;
		vecEnd.z -= 1024;

		tr = Controller.BuildTrace( vecStart, vecEnd ).Run();
		if ( tr.Fraction < 1 && tr.Normal.z >= 0.7f )
		{
			Controller.Velocity += new Vector3( 0, 0, 256 ) * WorldScale;
			WaterJumpTime = 2000;
		}
	}
}
