using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, FeatureEnabled( "Noclip" )] public bool EnableNoclipping { get; set; } = false;
	/// <summary>
	/// The Input Action that noclipping is triggered by.
	/// </summary>
	[Property, InputAction, Feature( "Noclip" )] public string NoclipAction { get; set; } = "Noclip";
	[Property, Feature( "Noclip" )] public bool OnlyAllowHost { get; set; } = true;
	[Sync, Change( "NoclipChange" )] public bool IsNoclipping { get; set; }


	public virtual void NoclipChange( bool oldValue, bool newValue )
	{
		if ( !Networking.IsHost )
		{
			if ( newValue == true ) IsNoclipping = false;
			return;
		}
		if ( newValue == true )
		{

			if ( Controller.PhysicsIntegration )
			{
				Controller.PhysicsBodyCollider.Enabled = false;
				Controller.PhysicsBodyRigidbody.Enabled = false;
				Controller.PhysicsShadowCollider.Enabled = false;
				Controller.PhysicsShadowRigidbody.Enabled = false;
			}
		}
		else
		{
			if ( Controller.PhysicsIntegration )
			{
				Controller.PhysicsBodyCollider.Enabled = true;
				Controller.PhysicsBodyRigidbody.Enabled = true;
				Controller.PhysicsShadowCollider.Enabled = true;
				Controller.PhysicsShadowRigidbody.Enabled = true;
			}
		}
	}
	/// <summary>
	/// TODO
	/// </summary>
	public virtual void CheckNoclip()
	{
		if ( Input.Pressed( NoclipAction ) ) IsNoclipping = !IsNoclipping;
	}

	/// <summary>
	/// TODO
	/// </summary>
	public virtual void DoNoclipMove()
	{
		Controller.IsOnGround = false;
		var wishvel = WishMove * EyeAngles.ToRotation();
		wishvel *= GetWishSpeed() * 5;

		if ( Input.Down( SwimUpAction ) ) wishvel = wishvel.WithZ( 300 );
		if ( Input.Down( SwimDownAction ) ) wishvel = wishvel.WithZ( -300 );

		Controller.WishVelocity = wishvel;
		Controller.Acceleration = Controller.BaseAcceleration;
		Controller.Accelerate( Controller.WishVelocity );
		Controller.Velocity = Controller.ApplyFriction( Controller.Velocity, 4, 100 );
		WorldPosition += Controller.Velocity * Time.Delta;
	}
}
