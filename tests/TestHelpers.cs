// Shared helpers for constructing PlayerInput structs in tests.
// Avoids repeating the 14-parameter constructor in every test method.
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;

namespace ResonanceOfSteel.Tests
{
	public static class TestHelpers
	{
		// Default positions: player at origin, opponent 5 units ahead on Z.
		public static readonly Fixed64 DefaultOwnX = Fixed64.Zero;
		public static readonly Fixed64 DefaultOwnZ = Fixed64.Zero;
		public static readonly Fixed64 DefaultOppX = Fixed64.Zero;
		public static readonly Fixed64 DefaultOppZ = (Fixed64)5.0;

		/// <summary>Empty input — no buttons pressed, no movement.</summary>
		public static PlayerInput EmptyInput() => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: false, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Attack press on this frame.</summary>
		public static PlayerInput AttackInput(AttackTier tier = AttackTier.Standard) => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: true,
			blockParryHeld: false, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: tier,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Block/parry pressed on this frame.</summary>
		public static PlayerInput BlockParryPressInput() => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: true, blockParryJustPressed: true,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Block held (not just pressed).</summary>
		public static PlayerInput BlockHeldInput() => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: true, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Dodge pressed with lateral movement (left).</summary>
		public static PlayerInput DodgeInput() => new(
			moveX: (Fixed64)(-1.0), moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: false, blockParryJustPressed: false,
			dodgeJustPressed: true, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Jump pressed.</summary>
		public static PlayerInput JumpInput() => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: false, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: true,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Movement toward opponent (forward on Z).</summary>
		public static PlayerInput MoveForwardInput(bool running = false) => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.One,
			runHeld: running,
			attackJustPressed: false,
			blockParryHeld: false, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Movement sideways (strafe, not toward opponent).</summary>
		public static PlayerInput MoveSidewaysInput() => new(
			moveX: Fixed64.One, moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: false, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Jump input but not grounded (airborne).</summary>
		public static PlayerInput AirborneInput() => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: false, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: false,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Grounded empty input (for jump landing check).</summary>
		public static PlayerInput GroundedInput() => EmptyInput();

		/// <summary>Dodge pressed but moving FORWARD (should be rejected).</summary>
		public static PlayerInput DodgeForwardInput() => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.One,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: false, blockParryJustPressed: false,
			dodgeJustPressed: true, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Block held + forward movement (block-walk scenario).</summary>
		public static PlayerInput BlockWalkForwardInput(bool running = false) => new(
			moveX: Fixed64.Zero, moveZ: Fixed64.One,
			runHeld: running,
			attackJustPressed: false,
			blockParryHeld: true, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Block held + lateral movement.</summary>
		public static PlayerInput BlockWalkSidewaysInput() => new(
			moveX: Fixed64.One, moveZ: Fixed64.Zero,
			runHeld: false,
			attackJustPressed: false,
			blockParryHeld: true, blockParryJustPressed: false,
			dodgeJustPressed: false, jumpJustPressed: false,
			modifierTier: AttackTier.Standard,
			isGrounded: true,
			ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
			opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
		);

		/// <summary>Run held + forward movement (standard run).</summary>
		public static PlayerInput RunForwardInput() => MoveForwardInput(running: true);

		/// <summary>Create a simulation with default constants and Longsword archetype.</summary>
		public static PlayerSimulation CreateSim(IArchetypeData archetype = null)
		{
			return new PlayerSimulation(
				EconomyConstants.Defaults,
				archetype ?? LongswordData.Instance);
		}

		/// <summary>Advance the simulation N frames with empty input.</summary>
		public static void TickN(PlayerSimulation sim, int n)
		{
			var empty = EmptyInput();
			for (int i = 0; i < n; i++)
				sim.Tick(empty);
		}

		/// <summary>Advance past Coil into Swing for a given tier.</summary>
		public static void AdvanceToSwing(PlayerSimulation sim, AttackTier tier, IArchetypeData archetype = null)
		{
			archetype ??= LongswordData.Instance;
			sim.Tick(AttackInput(tier));
			var move = archetype.GetMoveData(tier);
			var empty = EmptyInput();
			for (int i = 0; i < move.CoilFrames; i++)
				sim.Tick(empty);
		}

		/// <summary>Advance past Coil and Swing into Recovery for a given tier.</summary>
		public static void AdvanceToRecovery(PlayerSimulation sim, AttackTier tier, IArchetypeData archetype = null)
		{
			archetype ??= LongswordData.Instance;
			AdvanceToSwing(sim, tier, archetype);
			var move = archetype.GetMoveData(tier);
			var empty = EmptyInput();
			for (int i = 0; i < move.SwingFrames; i++)
				sim.Tick(empty);
		}

		/// <summary>Complete a full attack cycle (Coil → Swing → Recovery → Idle).</summary>
		public static void CompleteFullAttack(PlayerSimulation sim, AttackTier tier, IArchetypeData archetype = null)
		{
			archetype ??= LongswordData.Instance;
			AdvanceToRecovery(sim, tier, archetype);
			var empty = EmptyInput();
			while (!(sim.DebugStateName == "Idle"))
				sim.Tick(empty);
		}
	}
}
