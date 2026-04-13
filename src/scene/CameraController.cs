using Godot;

namespace ResonanceOfSteel.Scene
{
	public sealed partial class CameraController : Node3D
	{
		[Export] public Node3D OwnerCharacter;
		[Export] public Node3D OpponentCharacter;
		[Export] public float RotationSpeed = 5.0f;

		// Max spring arm distance — applied to the child SpringArm3D at startup.
		[Export] public float SpringArmLength = 5.0f;

		// Camera adjustment parameters.
		[Export] public float HeightScale = 5.0f; // How much the camera height changes based on distance.
		[Export] public float MinHeight = 0.5f; // Minimum height.
		[Export] public float MaxHeight = 2.0f; // Maximum height.
		[Export] public float HeightSpeed = 5.0f; // How quickly the camera height adjusts.
		[Export] public float MinPitch = -0.25f; // Minimum pitch angle in radians (looking down).
		[Export] public float MaxPitch = 0.5f; // Maximum pitch angle in radians (looking up).

		public override void _PhysicsProcess(double delta)
		{
			if (OwnerCharacter == null || OpponentCharacter == null) return;

			UpdateCameraPosition(delta);
			UpdateCameraRotation(delta);
		}

		private void UpdateCameraPosition(double delta)
		{
			// Lock the camera's horizontal position to the owner's position
			float currentHeight = GlobalPosition.Y;
			GlobalPosition = OwnerCharacter.GlobalPosition;

			// Calculate horizontal distance to opponent for height adjustment
			Vector3 toOpponent = OpponentCharacter.GlobalPosition - OwnerCharacter.GlobalPosition;
			toOpponent.Y = 0;
			float horizontalDistance = toOpponent.Length();

			// Compute target height based on distance (closer = higher camera)
			float targetHeight = HeightScale / (horizontalDistance + 0.001f); // Avoid division by zero
			targetHeight = Mathf.Clamp(targetHeight, MinHeight, MaxHeight);

			// Smoothly interpolate camera height
			float newHeight = Mathf.Lerp(currentHeight, OwnerCharacter.GlobalPosition.Y + targetHeight, HeightSpeed * (float)delta);
			GlobalPosition = GlobalPosition with { Y = newHeight };
		}

		private void UpdateCameraRotation(double delta)
		{
			// Calculate horizontal direction to opponent (ignore vertical component)
			Vector3 directionToOpponent = OpponentCharacter.GlobalPosition - OwnerCharacter.GlobalPosition;
			directionToOpponent.Y = 0; // Flatten to horizontal plane

			// Skip rotation if opponents are too close horizontally
			if (directionToOpponent.LengthSquared() < 0.001f) return;

			// Calculate vertical direction from camera to opponent
			Vector3 verticalDirectionToOpponent = OpponentCharacter.GlobalPosition - GlobalPosition;

			// Compute target yaw (horizontal rotation) and pitch (vertical tilt)
			float targetYaw = Mathf.Atan2(-directionToOpponent.X, -directionToOpponent.Z);
			float targetPitch = Mathf.Atan2(verticalDirectionToOpponent.Y, verticalDirectionToOpponent.Length());
			targetPitch = Mathf.Clamp(targetPitch, MinPitch, MaxPitch);

			// Get current rotation angles
			float currentYaw = GlobalRotation.Y;
			float currentPitch = GlobalRotation.X;

			// Calculate angle differences and interpolate smoothly
			float yawDifference = Mathf.AngleDifference(currentYaw, targetYaw);
			float pitchDifference = Mathf.AngleDifference(currentPitch, targetPitch);

			float newYaw = currentYaw + yawDifference * RotationSpeed * (float)delta;
			float newPitch = currentPitch + pitchDifference * RotationSpeed * (float)delta;

			GlobalRotation = GlobalRotation with { Y = newYaw, X = newPitch };
		}

		public override void _Ready()
		{
			TopLevel = true;
			var springArm = GetNodeOrNull<SpringArm3D>("SpringArm");
			if (springArm != null)
				springArm.SpringLength = SpringArmLength;
		}
	}
}