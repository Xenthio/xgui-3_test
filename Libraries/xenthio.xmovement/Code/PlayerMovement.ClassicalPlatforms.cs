using Sandbox;

namespace XMovement;

public partial class PlayerMovement : Component
{
	[Property, ShowIf( "PhysicsIntegration", false )] public bool DoClassicPlatforms { get; set; } = true;
	Transform GroundTransform;
	void RestoreGroundPos()
	{
		if ( !IsOnGround || !IsOnDynamicGeometry() || PhysicsIntegration || !DoClassicPlatforms )
			return;

		var transform = GroundObject.Transform.World.ToWorld( GroundTransform );
		PhysicsBodyVelocity = (transform.Position - WorldPosition) / Time.Delta;
		WorldRotation = WorldRotation.RotateAroundAxis( Vector3.Up, transform.Rotation.Yaw() - WorldRotation.Yaw() );
	}

	void SaveGroundPos()
	{
		if ( !IsOnGround || !IsOnDynamicGeometry() || PhysicsIntegration || !DoClassicPlatforms )
			return;

		GroundTransform = GroundObject.Transform.World.ToLocal( new Transform( WorldPosition, WorldRotation ) );
	}
}
