
// The authoritative simulation for one player.
// No Godot imports. No floats. No Node references.
using FixedMathSharp;
using ResonanceOfSteel.Simulation.States;

namespace ResonanceOfSteel.Simulation
{
	public class PlayerSimulation
	{
		// The current state of this player.
		private object _state;

		// All three resource systems.
		public EconomyHandler Economy { get; }

		// Position in world space (Fixed64 for determinism).
		public Fixed64 PosX { get; private set; }
		public Fixed64 PosZ { get; private set; }
		public Fixed64 FacingAngle { get; private set; }

		// The event generated this tick (consumed by the Bridge).
		public CombatEvent LastEvent { get; private set; }

		// Whether the hitbox should be active (set during Swing state).
		public bool HitboxActive { get; private set; }

		// The current attack tier (needed by Bridge for hitbox shape selection).
		public AttackTier CurrentTier { get; private set; }

		// Whether we are in a state where Tier 3 armor is active.
		public bool ArmorActive { get; private set; }

		// Tracks how many frames ago block was last pressed (any state).
		// Used by HitboxManager to evaluate the Shatter timing window.
		private int _blockPressedFramesAgo = int.MaxValue / 2;

		// Set by OnShatterWhiff(); applied as extra Recovery frames when the swing ends.
		private bool _shatterWhiffRecoveryPenalty = false;

		// Parry window frame data (how long the parry state lasts).
		private const int ParryWindowFrames = 6;
		// -4 frame disadvantage on Shatter whiff (Framework Section 4).
		private const int ShatterWhiffRecoveryPenaltyFrames = 4;

		// Shatter window equals the parry window — same timing requirement on both sides.
		public bool IsInShatterWindow() => _blockPressedFramesAgo <= ParryWindowFrames;
		private const int DodgeFrames = 18;
		private const int JumpFrames = 30;
		private const int StaggerFrames = 20;

		private readonly EconomyConstants _constants;
		private readonly IArchetypeData _archetype;
		public PlayerSimulation(EconomyConstants constants, IArchetypeData archetype)
		{
			_constants = constants;
			_archetype = archetype;
			Economy = new EconomyHandler(constants);
			_state = new Idle();
		}

		// ── Main entry point ───────────────────────────────────────────
		// Called exactly once per physics frame by the Bridge Layer.
		// input: the buffered input for this frame.
		// opponentPos: used for Right-of-Way Momentum calculation.
		public void Tick(PlayerInput input, Fixed64 opponentPosX, Fixed64 opponentPosZ)
		{
			LastEvent = CombatEvent.None;
			HitboxActive = false;
			ArmorActive = false;

			// Track how many frames ago block was last pressed (for Shatter window detection).
			// Uses JustPressed so holding block doesn't trivially satisfy the timing window.
			if (input.BlockParryJustPressed)
				_blockPressedFramesAgo = 0;
			else if (_blockPressedFramesAgo < int.MaxValue / 2)
				_blockPressedFramesAgo++;

			// Composure recovers every frame unless Terminal.
			Economy.TickComposureRecovery();

			// Right-of-Way Momentum: are we moving toward the opponent?
			var movingTowardOpponent = IsMovingToward(input, opponentPosX, opponentPosZ);
			if (movingTowardOpponent && (_state is Idle || _state is Moving))
				Economy.AddRightOfWayMomentum(input.RunHeld);

			// Delegate to state-specific logic.
			_state = ProcessState(input, _state);
		}

		// ── State processor ────────────────────────────────────────────
		private object ProcessState(PlayerInput input, object state)
		{
			return state switch
			{
				Idle s => ProcessIdle(input, s),
				Moving s => ProcessMoving(input, s),
				Coil s => ProcessCoil(input, s),
				Swing s => ProcessSwing(input, s),
				Recovery s => ProcessRecovery(input, s),
				Blocking s => ProcessBlocking(input, s),
				Parrying s => ProcessParrying(input, s),
				Dodging s => ProcessDodging(input, s),
				Jumping s => ProcessJumping(input, s),
				Staggered s => ProcessStaggered(input, s),
				Deathblow => new Deathblow(), // No inputs accepted in Deathblow
				_ => new Idle()
			};
		}

		// ── Individual state processors ────────────────────────────────

		private object ProcessIdle(PlayerInput input, Idle s)
		{
			// Check for movement
			if (HasMovementInput(input)) return new Moving(input.RunHeld);

			// Check for attack
			if (input.AttackPressed) return TransitionToCoil(input.ModifierTier);

			// Check for block/parry (press enters parry window; held enters block)
			if (input.BlockParryPressed) return new Parrying(ParryWindowFrames);

			// Check for dodge
			if (input.DodgePressed && Economy.CanAfford(_constants.DodgeCost))
			{
				Economy.SpendMomentum(_constants.DodgeCost);
				return new Dodging(DodgeFrames);
			}

			if (input.JumpPressed) return new Jumping(JumpFrames);

			return s; // Stay Idle
		}

		private object ProcessMoving(PlayerInput input, Moving s)
		{
			if (!HasMovementInput(input)) return new Idle();
			if (input.AttackPressed) return TransitionToCoil(input.ModifierTier);
			if (input.BlockParryPressed) return new Parrying(ParryWindowFrames);
			if (input.DodgePressed && Economy.CanAfford(_constants.DodgeCost))
			{
				Economy.SpendMomentum(_constants.DodgeCost);
				return new Dodging(DodgeFrames);
			}
			if (input.JumpPressed) return new Jumping(JumpFrames);
			return new Moving(input.RunHeld);
		}

		private object ProcessCoil(PlayerInput input, Coil s)
		{
			// No inputs accepted during Coil (Brief Section 5 table).
			CurrentTier = s.Tier;
			var framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0)
			{
				// Transition to Swing with the correct frame count for this tier.
				int swingFrames = GetSwingFrames(s.Tier);
				return new Swing(s.Tier, swingFrames, false);
			}
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessSwing(PlayerInput input, Swing s)
		{
			CurrentTier = s.Tier;
			HitboxActive = true;
			// Tier 3 armor is active during Swing frames (Brief Section 7.2).
			ArmorActive = (s.Tier == AttackTier.Super) && !Economy.IsFatigued;

			var framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0)
			{
				HitboxActive = false;
				int recoveryFrames = GetRecoveryFrames(s.Tier);
				if (_shatterWhiffRecoveryPenalty)
				{
					recoveryFrames += ShatterWhiffRecoveryPenaltyFrames;
					_shatterWhiffRecoveryPenalty = false;
				}
				return new Recovery(recoveryFrames, s.Tier);
			}
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessRecovery(PlayerInput input, Recovery s)
		{
			// Only block_parry is accepted during Recovery (Brief Section 5).
			if (input.BlockParryPressed) return new Blocking();

			var framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0) return new Idle();
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessBlocking(PlayerInput input, Blocking s)
		{
			if (!input.BlockParryPressed) return new Idle();
			return s;
		}

		private object ProcessParrying(PlayerInput input, Parrying s)
		{
			var framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0) return new Blocking(); // Window elapsed, now holding block
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessDodging(PlayerInput input, Dodging s)
		{
			// No inputs during Dodge.
			var framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0) return new Idle();
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessJumping(PlayerInput input, Jumping s)
		{
			// No inputs during Jump (landing handled by Bridge via physics).
			var framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0) return new Idle();
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessStaggered(PlayerInput input, Staggered s)
		{
			var framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0) return new Idle();
			return s with { FramesLeft = framesLeft };
		}

		// ── External event handlers (called by Bridge after hit detection) ───

		// Called when this character's hitbox connects with the opponent.
		public void OnHitLanded(Fixed64 vitalityMult, Fixed64 composureMult, bool wasBlocked)
		{
			Economy.AddMomentumOnHit();
			LastEvent = wasBlocked ? CombatEvent.HitBlocked : CombatEvent.HitLand;
			if (!wasBlocked)
				Economy.ResetFrameAdvantage(); // Attacker resets stacks on successful hit
		}

		// Called when this character IS HIT (defender perspective).
		public void OnHitReceived(Fixed64 vitalityMult, Fixed64 composureMult, bool wasBlocked)
		{
			Economy.ApplyVitalityDamage(vitalityMult);
			Economy.ApplyComposureDamage(composureMult);
			Economy.ResetFrameAdvantage();

			if (!wasBlocked)
				_state = new Staggered(StaggerFrames);

			// Check for Deathblow condition
			if (Economy.IsDeathblowVulnerable && !wasBlocked)
				_state = new Deathblow();
		}

		// Called on a successful Perfect Parry.
		public void OnParrySuccess()
		{
			Economy.SpendMomentum(_constants.PerfectParryCost);
			Economy.IncrementFrameAdvantage();
			LastEvent = CombatEvent.ParrySuccess;
		}

		// Called on a Clash (both attacks same tier simultaneously).
		public void OnClash()
		{
			Economy.AddClashSurge();
			LastEvent = CombatEvent.ClashEvent;
			_state = new Recovery(8, CurrentTier); // Short recovery after clash
		}

		public bool TryInitiateShatter()
		{
			if (!Economy.CanAfford(_constants.ShatterCost)) return false;
			Economy.SpendMomentum(_constants.ShatterCost);
			return true;
		}

		// Called when this character's Shatter lands (parry was broken).
		// Mirrors OnHitLanded but fires ShatterEvent instead of HitLand.
		public void OnShatterLanded()
		{
			Economy.AddMomentumOnHit();
			Economy.ResetFrameAdvantage();
			LastEvent = CombatEvent.ShatterEvent;
		}

		// Called when Shatter was attempted but the defender used Standard Block instead of Parry.
		// Per Framework Section 4: block occurs normally, attacker loses Momentum and suffers
		// a -4 frame Recovery penalty (disadvantage) on the current swing.
		public void OnShatterWhiff()
		{
			// Unconditional drain — the full cost is extracted even if Momentum runs out.
			Economy.SpendMomentum(_constants.ShatterCost);
			_shatterWhiffRecoveryPenalty = true;
			LastEvent = CombatEvent.ShatterWhiff;
		}

		// ── Helpers ────────────────────────────────────────────────────

		private object TransitionToCoil(AttackTier tier)
		{
			Economy.ResetFrameAdvantage(); // Attacking resets stacks (Brief Section 6.3)
			int coilFrames = GetCoilFrames(tier);
			int reduction = Economy.GetFrameAdvantageCoilReduction();
			int effective = System.Math.Max(1, coilFrames - reduction);
			CurrentTier = tier;
			return new Coil(tier, effective, reduction > 0);
		}

		private bool HasMovementInput(PlayerInput input)
			=> input.MoveX != Fixed64.Zero || input.MoveZ != Fixed64.Zero;

		private bool IsMovingToward(PlayerInput input, Fixed64 oppX, Fixed64 oppZ)
		{
			var dx = oppX - PosX;
			var dz = oppZ - PosZ;
			// Dot product of movement direction and direction-to-opponent
			return (input.MoveX * dx + input.MoveZ * dz) > Fixed64.Zero;
		}

		// Returns Coil frame count for this tier based on which archetype.
		private int GetCoilFrames(AttackTier tier) => _archetype.GetCoilFrames(tier);
		private int GetSwingFrames(AttackTier tier) => _archetype.GetSwingFrames(tier);
		private int GetRecoveryFrames(AttackTier tier) => _archetype.GetRecoveryFrames(tier);

		// Returns the current state type name (for debugging and UI).
		public string GetStateName() => _state?.GetType().Name ?? "Unknown";
	}
}

