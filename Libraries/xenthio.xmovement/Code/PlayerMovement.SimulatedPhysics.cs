﻿using Sandbox;
using Sandbox.Physics;
using System;
namespace XMovement;
public partial class PlayerMovement : Component
{
	[Sync]
	public Vector3 PhysicsBodyVelocity { get; set; }

	[ConVar] public static bool debug_playermovement { get; set; } = false;
	[Property, FeatureEnabled( "Physics Integration" )] public bool PhysicsIntegration { get; set; } = true;
	[Property, Feature( "Physics Integration" )] public float Mass { get; set; } = 85;
	[Property, Feature( "Physics Integration" )] public float PushScale { get; set; } = 0.7f;

	public Rigidbody PhysicsShadowRigidbody;
	public BoxCollider PhysicsShadowCollider;
	public Rigidbody PhysicsBodyRigidbody;
	public BoxCollider PhysicsBodyCollider;
	bool PreviouslyOnGround = false;

	void CreateShadowObjects()
	{
		if ( !PhysicsIntegration ) return;
		if ( IsProxy ) return;

		var pair = new CollisionRules.Pair( "movement", "movement" );

		if ( !this.GameObject.Scene.PhysicsWorld.CollisionRules.Pairs.ContainsKey( pair ) )
		{
			this.GameObject.Scene.PhysicsWorld.CollisionRules.Pairs.Add( pair, CollisionRules.Result.Ignore );
		}


		var shadow = Scene.CreateObject();
		shadow.SetParent( GameObject );
		shadow.Name = "PhysicsShadow";
		shadow.Tags.Add( "movement" );
		shadow.NetworkMode = NetworkMode.Object;

		var body = Scene.CreateObject();
		body.SetParent( GameObject );
		body.Name = "PhysicsBody";
		body.Tags.Add( "movement" );
		body.NetworkMode = NetworkMode.Object;


		PhysicsBodyRigidbody = body.Components.GetOrCreate<Rigidbody>();
		PhysicsBodyRigidbody.MassOverride = Mass;
		PhysicsBodyRigidbody.MassCenterOverride = Vector3.Zero;
		PhysicsBodyRigidbody.OverrideMassCenter = true;
		PhysicsBodyRigidbody.Gravity = false;
		PhysicsBodyRigidbody.Locking = new PhysicsLock() { Pitch = true, Roll = true };
		PhysicsBodyRigidbody.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;
		PhysicsBodyCollider = body.Components.GetOrCreate<BoxCollider>();
		PhysicsBodyCollider.Scale = BoundingBox.Maxs + BoundingBox.Mins.Abs();
		PhysicsBodyCollider.Center = BoundingBox.Center;

		PhysicsShadowRigidbody = shadow.Components.GetOrCreate<Rigidbody>();
		PhysicsShadowRigidbody.MassOverride = Mass;
		PhysicsShadowRigidbody.Locking = new PhysicsLock() { Pitch = true, Yaw = true, Roll = true };
		PhysicsShadowRigidbody.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;
		PhysicsShadowCollider = shadow.Components.GetOrCreate<BoxCollider>();
		PhysicsShadowCollider.Scale = BoundingBox.Maxs + BoundingBox.Mins.Abs();
		PhysicsShadowCollider.Center = BoundingBox.Center;

		var surf = Surface.FindByName( "xmove_phys" );

		PhysicsBodyCollider.Surface = surf;
		PhysicsShadowCollider.Surface = surf;

		shadow.NetworkSpawn();
		body.NetworkSpawn();
	}
	void ResetSimulatedShadow()
	{
		if ( IsProxy ) return;
		if ( !PhysicsIntegration ) return;
		if ( !PhysicsBodyRigidbody.IsValid() ) return;
		if ( !PhysicsShadowRigidbody.IsValid() ) return;
		if ( !PhysicsBodyRigidbody.Enabled ) return;
		if ( !PhysicsShadowRigidbody.Enabled ) return;
		var shvel = Velocity * 1f;
		var whvel = WishVelocity * PushScale;

		shvel.x = MathF.MaxMagnitude( shvel.x, whvel.x );
		shvel.y = MathF.MaxMagnitude( shvel.y, whvel.y );
		shvel.z = MathF.MaxMagnitude( shvel.z, whvel.z );
		PhysicsShadowRigidbody.Velocity = shvel;

		PhysicsShadowCollider.Scale = BoundingBox.Maxs + BoundingBox.Mins.Abs();
		PhysicsShadowCollider.Center = BoundingBox.Center;

		PhysicsBodyCollider.Scale = BoundingBox.Maxs + BoundingBox.Mins.Abs();
		PhysicsBodyCollider.Center = BoundingBox.Center;


		if ( debug_playermovement ) DebugOverlay.Box( PhysicsShadowRigidbody.PhysicsBody.GetBounds(), Color.Blue, 1 );
		if ( debug_playermovement ) DebugOverlay.Box( PhysicsBodyRigidbody.PhysicsBody.GetBounds(), Color.Green, 1 );
	}
	void UpdateFromSimulatedShadow()
	{
		if ( !PhysicsIntegration ) return;
		if ( !PhysicsBodyRigidbody.IsValid() ) return;
		if ( !PhysicsShadowRigidbody.IsValid() ) return;
		if ( !PhysicsBodyRigidbody.Enabled ) return;
		if ( !PhysicsShadowRigidbody.Enabled ) return;

		// We do this so we don't slide down things and off ledges on static geometry or as soon as we land.
		bool OnDynamicGeometry = IsOnDynamicGeometry();
		if ( !OnDynamicGeometry )
		{
			PhysicsBodyRigidbody.AngularVelocity = Vector3.Zero;
			PhysicsBodyRigidbody.LocalRotation = Rotation.Identity;
		}

		//if ( DoGravity ) PhysicsBodyRigidbody.Velocity += Gravity * Time.Delta * 0.5f;
		var vel = PhysicsBodyRigidbody.Velocity;

		/*if ( IsStuck() )
		{
			//vel.z = MathF.Max( 0, vel.z );
			//WorldPosition += vel * Time.Delta;
		}*/
		if ( IsStuck() && PhysicsBodyRigidbody.WorldPosition != Vector3.Zero )
		{
			// If we're stuck, assume the physics simulated body is where we should be.
			var stuckpos = WorldPosition;
			var bodypos = PhysicsBodyRigidbody.WorldPosition;
			var delta = bodypos - stuckpos;
			WorldPosition = bodypos;// IsOnGround && !OnDynamicGeometry ? bodypos.WithZ( WorldPosition.z ) : bodypos;
									//MoveTo( bodypos, true );
			PhysicsBodyVelocity = delta;
		}
		else
		{
			PhysicsBodyVelocity = vel;// * Time.Delta;   
		}

		TryUnstuck();

		//if ( GroundObject == null && PreviouslyOnGround )
		//{
		//	PhysicsShadowVelocity = Vector3.Zero;
		//	PhysicsBodyRigidbody.Velocity = Vector3.Zero;
		//	Velocity += vel;
		//}

		if ( OnDynamicGeometry )
		{
			var a = PhysicsBodyRigidbody.AngularVelocity;
			var angles = new Angles( a.x, a.y, a.z );

			var axis = a.WithX( 0 ).WithY( 0 );

			var length = axis.Length;

			if ( MovementFrequency == MovementFrequencyMode.PerFixedUpdate ) length *= Time.Delta;
			if ( MovementFrequency == MovementFrequencyMode.PerUpdate ) length *= 1f;
			if ( length > 0 && length < 1 )
			{
				GameObject.WorldRotation = GameObject.WorldRotation.RotateAroundAxis( axis.Normal, length );
				GameObject.WorldRotation = GameObject.WorldRotation.RotateAroundAxis( Vector3.Up, PhysicsBodyRigidbody.WorldRotation.Angles().yaw );
			}
		}

		PhysicsBodyRigidbody.AngularVelocity = Vector3.Zero;
		PhysicsShadowRigidbody.AngularVelocity = Vector3.Zero;

		if ( OnDynamicGeometry )
		{
			PhysicsBodyRigidbody.Velocity -= Gravity * Time.Delta * 1f;
		}
		else if ( !IsStuck() && IsOnGround )
		{
			// we need "friction" here since we've got no gravity pulling us down, else we will keep sliding once pushed.
			PhysicsBodyRigidbody.Velocity = ApplyFriction( PhysicsBodyRigidbody.Velocity, GetFriction() * 1f, 0f );
		}

		// If we're on static ground, we want to stay on that ground, so lock moving up.
		var locking = PhysicsBodyRigidbody.Locking;
		locking.Z = IsOnGround && !OnDynamicGeometry;
		locking.Yaw = IsOnGround && !OnDynamicGeometry;
		PhysicsBodyRigidbody.Locking = locking;

		PreviouslyOnGround = GroundObject != null;
		ResetPhysSimPosition();
		ResetPhysSimVelocity();
	}

	bool IsOnDynamicGeometry()
	{
		bool onDynamicGeometry = true;
		if ( (GroundObject.IsValid() &&
		(
		GroundObject.Components.TryGet<MapCollider>( out var mapCollider ) ||
		GroundObject.Components.TryGet<Collider>( out var col ) && col.Static
		)
		)
		|| !GroundObject.IsValid() )
		{
			onDynamicGeometry = false;
		}
		return onDynamicGeometry;
	}

	void ResetPhysSimVelocity()
	{
		PhysicsShadowRigidbody.AngularVelocity = Vector3.Zero;
		PhysicsShadowRigidbody.Velocity = Vector3.Zero;

		PhysicsBodyRigidbody.AngularVelocity = Vector3.Zero;
		//PhysicsBodyRigidbody.Velocity = Vector3.Zero;
	}
	void ResetPhysSimPosition()
	{
		PhysicsShadowRigidbody.LocalPosition = Vector3.Zero;
		PhysicsShadowRigidbody.WorldRotation = Rotation.Identity;

		PhysicsBodyRigidbody.LocalPosition = Vector3.Zero;
		PhysicsBodyRigidbody.WorldRotation = Rotation.Identity;
	}
}
