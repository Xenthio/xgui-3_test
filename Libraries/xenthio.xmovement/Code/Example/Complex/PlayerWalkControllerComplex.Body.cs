using Sandbox;
using Sandbox.Citizen;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, Group( "Body" )] public GameObject Body { get; set; }
	[Property, Group( "Body" )] public SkinnedModelRenderer BodyModelRenderer { get; set; }
	[RequireComponent] public CitizenAnimationHelper AnimationHelper { get; set; }

	protected void SetupBody()
	{
		if ( !Body.IsValid() )
		{
			Body = Scene.CreateObject();
			Body.SetParent( GameObject );
			Body.LocalPosition = Vector3.Zero;
			Body.Name = "Body";
		}
		if ( !BodyModelRenderer.IsValid() )
		{
			BodyModelRenderer = Body.AddComponent<SkinnedModelRenderer>();
			BodyModelRenderer.Model = Model.Load( "models/citizen/citizen.vmdl" );
			AnimationHelper.Target = BodyModelRenderer;
		}
	}
	[Property, Group( "Animator" )] public float RotationAngleLimit { get; set; } = 45.0f;
	[Property, Group( "Animator" )] public float RotationSpeed { get; set; } = 1.0f;
	[Property, Group( "Animator" )] public bool RotationFaceLadders { get; set; } = true;
	float _animRotationSpeed;
	TimeSince timeSinceRotationSpeedUpdate;

	public virtual void RotateBody()
	{
		if ( IsTouchingLadder && RotationFaceLadders )
		{
			Body.WorldRotation = Rotation.Lerp( Body.WorldRotation, Rotation.LookAt( LadderNormal * -1 ), Time.Delta * 5.0f );
			return;
		}
		var targetAngle = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();

		var velocity = Controller.WishVelocity.WithZ( 0 );

		float rotateDifference = BodyModelRenderer.WorldRotation.Distance( targetAngle );

		// We're over the limit - snap it 
		if ( rotateDifference > RotationAngleLimit )
		{
			var delta = 0.999f - (RotationAngleLimit / rotateDifference);
			var newRotation = Rotation.Lerp( BodyModelRenderer.WorldRotation, targetAngle, delta );

			var a = newRotation.Angles();
			var b = BodyModelRenderer.WorldRotation.Angles();

			var yaw = MathX.DeltaDegrees( a.yaw, b.yaw );

			_animRotationSpeed += yaw;
			_animRotationSpeed = _animRotationSpeed.Clamp( -90, 90 );

			BodyModelRenderer.WorldRotation = newRotation;
		}

		if ( velocity.Length > 10 )
		{
			var newRotation = Rotation.Slerp( BodyModelRenderer.WorldRotation, targetAngle, Time.Delta * 2.0f * RotationSpeed * velocity.Length.Remap( 0, 100 ) );

			var a = newRotation.Angles();
			var b = BodyModelRenderer.WorldRotation.Angles();

			var yaw = MathX.DeltaDegrees( a.yaw, b.yaw );

			_animRotationSpeed += yaw;
			_animRotationSpeed = _animRotationSpeed.Clamp( -90, 90 );

			BodyModelRenderer.WorldRotation = newRotation;
		}
	}
	public virtual void Animate()
	{
		AnimationHelper.WithWishVelocity( Controller.WishVelocity );
		AnimationHelper.WithVelocity( Controller.Velocity );

		// skid, this isn't in AnimationHelper yet it seems? ok nvm it's absolutely FUCKED
		/*{
			var dir = Controller.Velocity.SubtractDirection( Controller.WishVelocity.Normal );
			if ( dir.IsNearlyZero( 1.0f ) ) dir = 0;

			var forward = BodyModelRenderer.WorldRotation.Forward.Dot( dir );
			var sideward = BodyModelRenderer.WorldRotation.Right.Dot( dir );

			BodyModelRenderer.Set( "skid_x", forward );
			BodyModelRenderer.Set( "skid_y", sideward );

			var skidAmount = (Controller.Velocity.Length - Controller.WishVelocity.Length).Clamp( 0, 10 ).Remap( 0, 10, 0, 0.5f );
			BodyModelRenderer.Set( "skid", skidAmount );
		}*/

		AnimationHelper.WithLook( EyeAngles.Forward * 100, 1, 1, 1.0f );
		AnimationHelper.DuckLevel = IsCrouching ? 100 : 0;
		AnimationHelper.IsGrounded = Controller.IsOnGround || IsTouchingLadder;
		AnimationHelper.IsClimbing = IsTouchingLadder;
		AnimationHelper.IsSwimming = IsSwimming;
		AnimationHelper.IsNoclipping = IsNoclipping;

		if ( timeSinceRotationSpeedUpdate > 0.1f )
		{
			timeSinceRotationSpeedUpdate = 0;
			AnimationHelper.MoveRotationSpeed = _animRotationSpeed * 5;
			_animRotationSpeed = 0;
		}
		RotateBody();
	}
}
