using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	/// <summary>
	/// Per-frame input state sampled by the Bridge.
	/// MoveX/MoveZ are world-space direction (camera-transformed, normalized) — not raw axes.
	/// The Simulation owns the InputBuffer and performs its own consumption.
	/// "JustPressed" fields are edge-detected (true only on the frame the button transitions to pressed).
	/// </summary>
	public readonly struct PlayerInput
	{
		public readonly Fixed64 MoveX;
		public readonly Fixed64 MoveZ;
		public readonly bool RunHeld;
		public readonly bool AttackJustPressed;
		public readonly bool BlockParryHeld;
		public readonly bool BlockParryJustPressed;
		public readonly bool DodgeJustPressed;
		public readonly bool JumpJustPressed;
		public readonly AttackTier ModifierTier;
		public readonly bool IsGrounded;
		public readonly Fixed64 OwnPosX;
		public readonly Fixed64 OwnPosZ;
		public readonly Fixed64 OpponentPosX;
		public readonly Fixed64 OpponentPosZ;

		public PlayerInput(
			Fixed64 moveX, Fixed64 moveZ,
			bool runHeld,
			bool attackJustPressed,
			bool blockParryHeld, bool blockParryJustPressed,
			bool dodgeJustPressed, bool jumpJustPressed,
			AttackTier modifierTier,
			bool isGrounded,
			Fixed64 ownPosX, Fixed64 ownPosZ,
			Fixed64 opponentPosX, Fixed64 opponentPosZ)
		{
			MoveX = moveX;
			MoveZ = moveZ;
			RunHeld = runHeld;
			AttackJustPressed = attackJustPressed;
			BlockParryHeld = blockParryHeld;
			BlockParryJustPressed = blockParryJustPressed;
			DodgeJustPressed = dodgeJustPressed;
			JumpJustPressed = jumpJustPressed;
			ModifierTier = modifierTier;
			IsGrounded = isGrounded;
			OwnPosX = ownPosX;
			OwnPosZ = ownPosZ;
			OpponentPosX = opponentPosX;
			OpponentPosZ = opponentPosZ;
		}

		public bool HasMovement => MoveX != Fixed64.Zero || MoveZ != Fixed64.Zero;
	}
}

