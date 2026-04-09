using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	// AttackTier maps to the modifier buttons defined in the Prototype Brief Section 4.
	// None means no modifier was held when attack was pressed.
	public enum AttackTier { Light, Standard, Heavy, Super }

	// PlayerInputAction is what gets stored in the TTL input buffer.
	// See Prototype Brief Section 4.2 for priority ordering.
	public enum PlayerInputAction
	{
		None,
		Attack,
		BlockParry,
		Dodge,
		Jump,
		Run,
	}

	// PlayerInput is a C# record (immutable value type).
	// Created fresh each physics frame by the Bridge Layer.
	public record PlayerInput(
		Fixed64 MoveX,          // Left stick horizontal, -1 to 1
		Fixed64 MoveZ,          // Left stick vertical, -1 to 1
		bool RunHeld,           // R2/RT held
		bool AttackPressed,     // R1/RB pressed this frame
		bool BlockParryPressed, // L1/LB pressed or held this frame
		bool BlockParryJustPressed, // L1/LB pressed this frame only (not held)
		bool DodgePressed,      // L2/LT pressed this frame
		bool JumpPressed,       // A/Cross pressed this frame
		AttackTier ModifierTier // Which modifier was held when attack pressed
	);

	public enum ArchetypeType { Longsword, Greatsword }
}

