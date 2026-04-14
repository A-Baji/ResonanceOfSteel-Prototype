
// The authoritative simulation for one player.
// No Godot imports. No floats. No Node references.
// PHASE 2 NOTE: The `private object _state` pattern and record-based state machine
// will be replaced by Chickensoft LogicBlocks (v5.20.0, already imported) which provides:
//   - Compile-time exhaustive state transitions via hierarchical statecharts
//   - Native MemoryPack serialization for rollback snapshots
//   - Auto-generated state diagrams
// Until then, the object+switch pattern is intentional and functional.
// Do NOT refactor the state representation without migrating to LogicBlocks.
using System;
using FixedMathSharp;
using ResonanceOfSteel.Simulation.Archetypes;
using ResonanceOfSteel.Simulation.States;

namespace ResonanceOfSteel.Simulation
{
	public sealed class PlayerSimulation
	{
		// ── State ──────────────────────────────────────────────────────
		// See PHASE 2 NOTE at top of file regarding LogicBlocks migration.
		private object _state;

		// ── Economy ────────────────────────────────────────────────────
		public EconomyHandler Economy { get; }

		// ── Deterministic position (synced each Tick from Bridge) ───────
		public Fixed64 PosX { get; private set; }
		public Fixed64 PosZ { get; private set; }

		// Previous-frame position for computing actual displacement (RoW).
		private Fixed64 _prevPosX, _prevPosZ;
		private bool _posInitialized;

		// ── Per-tick outputs (consumed by Bridge after Tick) ────────────
		public CombatEvent LastEvent { get; private set; }
		public bool HitboxActive { get; private set; }
		public AttackTier CurrentTier { get; private set; }
		public bool ArmorActive { get; private set; }

		// ── Semantic state queries (used by Bridge/CombatResolver) ──────
		public bool IsBlocking => _state is Blocking;
		public bool IsParrying => _state is Parrying;
		public bool IsInDeathblow => _state is Deathblow;
		public bool IsStaggered => _state is Staggered;
		public bool IsSwinging => _state is Swing;

		/// <summary>
		/// True during any committed action (attacks, evasion, stagger, deathblow).
		/// Blocks input consumption and suppresses input-driven movement (slide-to-stop).
		/// </summary>
		public bool IsActionLocked => _state is not (Idle or Moving or Blocking);

		// ── Debug properties (read by DebugHUD via Bridge) ──────────────
		public int DebugBufferCount => _buffer.Count;

		public string DebugStateName => _state switch
		{
			Idle => "Idle",
			Moving m => m.IsRunning ? "Running" : "Moving",
			Coil c => $"Coil T{(int)c.Tier} [{c.FramesLeft}f]",
			Swing s => $"Swing T{(int)s.Tier} [{s.FramesLeft}f]",
			Recovery r => $"Recovery T{(int)r.Tier} [{r.FramesLeft}f]",
			Blocking => "Blocking",
			Parrying p => $"Parrying [{p.FramesLeft}f]",
			Dodging d => d.StartupLeft > 0 ? $"Dodge Startup [{d.StartupLeft}f]"
				: d.ActiveLeft > 0 ? $"Dodge Active [{d.ActiveLeft}f]"
				: $"Dodge Recovery [{d.RecoveryLeft}f]",
			Jumping j => j.StartupLeft > 0 ? $"Jump Startup [{j.StartupLeft}f]"
				: j.ActiveLeft > 0 ? $"Jump Active [{j.ActiveLeft}f]"
				: $"Jump Recovery [{j.RecoveryLeft}f]",
			Staggered s => $"Staggered [{s.FramesLeft}f]",
			Deathblow => "DEATHBLOW",
			_ => "Unknown"
		};

		// ── Input buffer (owned by Simulation for rollback serialization)
		private readonly InputBuffer _buffer;

		// ── Shatter timing ─────────────────────────────────────────────
		private int _blockPressedFramesAgo = int.MaxValue / 2;
		public bool IsInShatterWindow => _blockPressedFramesAgo < EffectiveParryWindowFrames;

		// ── Premature press penalty queries ─────────────────────────────
		public int PrematureBlockPenalties => _prematureBlockPenalties;

		/// <summary>
		/// Parry/shatter window after premature press penalties.
		/// Each whiffed parry or shatter halves the window (ceil, min 1).
		/// Resets on successful parry or shatter.
		/// </summary>
		public int EffectiveParryWindowFrames
		{
			get
			{
				int window = _constants.ParryWindowFrames;
				for (int i = 0; i < _prematureBlockPenalties; i++)
				{
					window = (window + 1) / 2;
					if (window <= 1) return 1;
				}
				return window;
			}
		}

		// ── Shatter whiff penalty ──────────────────────────────────────
		private bool _shatterWhiffRecoveryPending;

		// ── Premature press penalty ────────────────────────────────────
		// Tracks failed parry/shatter attempts. Each whiff halves the
		// effective window (ceil, min 1). Resets on success or inactivity.
		private int _prematureBlockPenalties;
		private bool _parrySucceededThisAttempt;

		// Frames of no block_parry press before penalty resets (Sekiro-style).
		private const int PenaltyInactivityResetFrames = 30;

		// ── Clash double-processing guard ──────────────────────────────
		public bool ClashedThisFrame { get; private set; }

		private readonly EconomyConstants _constants;
		private readonly IArchetypeData _archetype;

		public PlayerSimulation(EconomyConstants constants, IArchetypeData archetype)
		{
			_constants = constants;
			_archetype = archetype;
			Economy = new EconomyHandler(constants);
			_buffer = new InputBuffer(constants.InputBufferTTL);
			_state = new Idle();
		}

		// ── Main entry point ───────────────────────────────────────────
		// Called exactly once per physics frame by the Bridge Layer.
		public void Tick(PlayerInput input)
		{
			// Reset per-tick outputs.
			LastEvent = CombatEvent.None;
			HitboxActive = false;
			ArmorActive = false;
			ClashedThisFrame = false;

			// Sync authoritative position from Bridge.
			PosX = input.OwnPosX;
			PosZ = input.OwnPosZ;

			// Initialize previous position on first tick to avoid bogus displacement.
			if (!_posInitialized)
			{
				_prevPosX = input.OwnPosX;
				_prevPosZ = input.OwnPosZ;
				_posInitialized = true;
			}

			// Track block press recency for Shatter window detection.
			if (input.BlockParryJustPressed)
				_blockPressedFramesAgo = 0;
			else if (_blockPressedFramesAgo < int.MaxValue / 2)
				_blockPressedFramesAgo++;

			// Premature press penalty decays after inactivity (Sekiro-style).
			// If the player stops pressing block_parry for long enough, reset.
			if (_blockPressedFramesAgo >= PenaltyInactivityResetFrames && _prematureBlockPenalties > 0)
				_prematureBlockPenalties = 0;

			// Composure recovers every frame unless Terminal.
			Economy.TickComposureRecovery();

			// Frame Advantage Stack gradual decay.
			Economy.TickStackDecay();

			// Right-of-Way Momentum: displacement-based with range limit.
			if (_state is Idle || _state is Moving || _state is Blocking)
			{
				var dispX = input.OwnPosX - _prevPosX;
				var dispZ = input.OwnPosZ - _prevPosZ;
				var dispSq = dispX * dispX + dispZ * dispZ;

				if (dispSq > Fixed64.Zero)
				{
					var toOppX = input.OpponentPosX - input.OwnPosX;
					var toOppZ = input.OpponentPosZ - input.OwnPosZ;
					var distSq = toOppX * toOppX + toOppZ * toOppZ;

					if (distSq > Fixed64.Zero && distSq <= _constants.RoWMaxRangeSquared)
					{
						// Project displacement onto opponent direction.
						var dot = dispX * toOppX + dispZ * toOppZ;
						var dist = FixedMath.Sqrt(distSq);
						var towardDisp = dot / dist; // positive = toward, negative = away

						if (towardDisp > Fixed64.Zero)
							Economy.AddRightOfWayMomentum(towardDisp);
						else if (towardDisp < Fixed64.Zero)
							Economy.DrainRetreatMomentum(-towardDisp);
					}
				}
			}

			// Update previous position for next frame's displacement calculation.
			_prevPosX = input.OwnPosX;
			_prevPosZ = input.OwnPosZ;

			// Buffer management: enqueue new presses, consume only in actionable states, then age.
			EnqueueInputs(input);
			var consumed = (_state is Idle or Moving) ? _buffer.Consume() : PlayerInputAction.None;
			_buffer.Tick();

			// Delegate to state-specific logic.
			_state = ProcessState(input, consumed);
		}

		// ── Input buffer bridge ────────────────────────────────────────
		private void EnqueueInputs(PlayerInput input)
		{
			if (input.AttackJustPressed) _buffer.Add(PlayerInputAction.Attack);
			if (input.BlockParryJustPressed) _buffer.Add(PlayerInputAction.BlockParry);
			if (input.DodgeJustPressed) _buffer.Add(PlayerInputAction.Dodge);
			if (input.JumpJustPressed) _buffer.Add(PlayerInputAction.Jump);
		}

		// ── State processor ────────────────────────────────────────────
		private object ProcessState(PlayerInput input, PlayerInputAction consumed)
		{
			return _state switch
			{
				Idle => ProcessIdle(input, consumed),
				Moving => ProcessMoving(input, consumed),
				Coil s => ProcessCoil(s),
				Swing s => ProcessSwing(s),
				Recovery s => ProcessRecovery(s),
				Blocking => ProcessBlocking(input),
				Parrying s => ProcessParrying(s),
				Dodging s => ProcessDodging(s),
				Jumping s => ProcessJumping(input, s),
				Staggered s => ProcessStaggered(s),
				Deathblow => new Deathblow(),
				// PHASE 2: LogicBlocks migration will make this branch unreachable
				// via compile-time exhaustiveness. Until then, this is a safety net.
				_ => new Idle()
			};
		}

		// ── Actionable state input processing ──────────────────────────

		private object ProcessIdle(PlayerInput input, PlayerInputAction consumed)
		{
			return ProcessActionableInput(input, consumed)
				?? (input.HasMovement ? new Moving(input.RunHeld) : (object)new Idle());
		}

		private object ProcessMoving(PlayerInput input, PlayerInputAction consumed)
		{
			if (!input.HasMovement) return new Idle();
			return ProcessActionableInput(input, consumed) ?? new Moving(input.RunHeld);
		}

		/// <summary>
		/// Shared logic for states that accept all input (Idle, Moving).
		/// Returns a new state if an action was consumed, or null to remain in current state.
		/// </summary>
		private object ProcessActionableInput(PlayerInput input, PlayerInputAction consumed)
		{
			switch (consumed)
			{
				case PlayerInputAction.BlockParry:
					if (Economy.CanAfford(_constants.PerfectParryCost))
					{
						_parrySucceededThisAttempt = false;
						return new Parrying(EffectiveParryWindowFrames);
					}
					return new Blocking();

				case PlayerInputAction.Attack:
					return TransitionToCoil(input.ModifierTier);

				case PlayerInputAction.Dodge:
					if (input.HasMovement && !IsMovingToward(input))
					{
						bool canAffordDodge = Economy.CanAfford(_constants.DodgeCost);
						if (canAffordDodge) Economy.SpendMomentum(_constants.DodgeCost);
						int startupPenalty = canAffordDodge ? 0 : _constants.EvasionFatigueStartupPenalty;
						int activeReduction = canAffordDodge ? 0 : _constants.EvasionFatigueActiveReduction;
						return new Dodging(
							_constants.DodgeStartupFrames + startupPenalty,
							Math.Max(1, _constants.DodgeActiveFrames - activeReduction),
							_constants.DodgeRecoveryFrames);
					}
					return null;

				case PlayerInputAction.Jump:
					{
						bool canAffordJump = Economy.CanAfford(_constants.JumpCost);
						if (canAffordJump) Economy.SpendMomentum(_constants.JumpCost);
						int startupPenalty = canAffordJump ? 0 : _constants.EvasionFatigueStartupPenalty;
						int activeReduction = canAffordJump ? 0 : _constants.EvasionFatigueActiveReduction;
						return new Jumping(
							_constants.JumpStartupFrames + startupPenalty,
							Math.Max(1, _constants.JumpActiveFrames - activeReduction),
							_constants.JumpRecoveryFrames);
					}

				default:
					if (input.BlockParryHeld)
						return new Blocking();
					return null;
			}
		}

		// ── Attack phase processors ────────────────────────────────────

		private object ProcessCoil(Coil s)
		{
			CurrentTier = s.Tier;
			int framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0)
			{
				var move = _archetype.GetMoveData(s.Tier);
				return new Swing(s.Tier, move.SwingFrames);
			}
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessSwing(Swing s)
		{
			CurrentTier = s.Tier;
			HitboxActive = true;
			ArmorActive = s.Tier == AttackTier.Super && !Economy.IsFatigued;

			int framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0)
			{
				HitboxActive = false;
				var move = _archetype.GetMoveData(s.Tier);
				int recoveryFrames = move.RecoveryFrames;
				if (_shatterWhiffRecoveryPending)
				{
					recoveryFrames += _constants.ShatterWhiffPenaltyFrames;
					_shatterWhiffRecoveryPending = false;
				}
				return new Recovery(recoveryFrames, s.Tier);
			}
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessRecovery(Recovery s)
		{
			int framesLeft = s.FramesLeft - 1;
			return framesLeft <= 0 ? new Idle() : s with { FramesLeft = framesLeft };
		}

		// ── Defensive state processors ─────────────────────────────────

		private object ProcessBlocking(PlayerInput input)
		{
			return input.BlockParryHeld ? new Blocking() : new Idle();
		}

		private object ProcessParrying(Parrying s)
		{
			int framesLeft = s.FramesLeft - 1;
			if (framesLeft <= 0)
			{
				if (!_parrySucceededThisAttempt)
					_prematureBlockPenalties++;
				return new Blocking();
			}
			return s with { FramesLeft = framesLeft };
		}

		private object ProcessDodging(Dodging s)
		{
			if (s.StartupLeft > 0) return s with { StartupLeft = s.StartupLeft - 1 };
			if (s.ActiveLeft > 0) return s with { ActiveLeft = s.ActiveLeft - 1 };
			if (s.RecoveryLeft > 0) return s with { RecoveryLeft = s.RecoveryLeft - 1 };
			return new Idle();
		}

		private object ProcessJumping(PlayerInput input, Jumping s)
		{
			if (s.StartupLeft > 0) return s with { StartupLeft = s.StartupLeft - 1 };
			if (s.ActiveLeft > 0)
			{
				// Ground check during active phase — early landing skips to recovery.
				if (input.IsGrounded) return s with { ActiveLeft = 0 };
				return s with { ActiveLeft = s.ActiveLeft - 1 };
			}
			if (s.RecoveryLeft > 0) return s with { RecoveryLeft = s.RecoveryLeft - 1 };
			return new Idle();
		}

		// ── Damage state processors ────────────────────────────────────

		private object ProcessStaggered(Staggered s)
		{
			int framesLeft = s.FramesLeft - 1;
			return framesLeft <= 0 ? new Idle() : s with { FramesLeft = framesLeft };
		}

		// ── Combat event handlers (called by CombatResolver / Bridge) ──

		public void OnHitLanded(Fixed64 vitalityMult, Fixed64 composureMult, bool wasBlocked)
		{
			Economy.AddMomentumOnHit();
			LastEvent = wasBlocked ? CombatEvent.HitBlocked : CombatEvent.HitLand;
		}

		public void OnHitReceived(Fixed64 vitalityMult, Fixed64 composureMult,
			bool wasBlocked, AttackTier attackerTier, int staggerFrames)
		{
			if (attackerTier == AttackTier.Light)
			{
				if (wasBlocked)
					Economy.ApplyComposureDamage(composureMult);
				else
					Economy.ApplyVitalityDamage(vitalityMult);
			}
			else if (wasBlocked)
			{
				// Chip damage: blocked T1-T3 attacks deal 20% vitality + full composure.
				Economy.ApplyVitalityDamage(vitalityMult * _constants.ChipDamageMultiplier);
				Economy.ApplyComposureDamage(composureMult);
			}
			else
			{
				Economy.ApplyVitalityDamage(vitalityMult);
				Economy.ApplyComposureDamage(composureMult);
			}

			Economy.ResetFrameAdvantage();

			// Deathblow triggers on ANY hit (blocked or not) once vulnerable.
			// Vitality ≤ 0 or Composure ≥ 1.0 — blocking cannot prevent execution.
			if (Economy.IsDeathblowVulnerable)
				_state = new Deathblow();
			else if (!wasBlocked && staggerFrames > 0)
				_state = new Staggered(staggerFrames);
		}

		public void OnParrySuccess()
		{
			_parrySucceededThisAttempt = true;
			_prematureBlockPenalties = 0;
			Economy.SpendMomentum(_constants.PerfectParryCost);
			Economy.IncrementFrameAdvantage();
			LastEvent = CombatEvent.ParrySuccess;
		}

		public void OnDeathblowTriggered()
		{
			LastEvent = CombatEvent.DeathblowTriggered;
			_state = new Deathblow();
		}

		public void OnClash()
		{
			if (ClashedThisFrame) return;
			ClashedThisFrame = true;
			Economy.AddClashSurge();
			LastEvent = CombatEvent.ClashEvent;
			_state = new Recovery(_constants.ClashRecoveryFrames, CurrentTier);
		}

		public bool TryInitiateShatter()
		{
			if (!Economy.CanAfford(_constants.ShatterCost)) return false;
			Economy.SpendMomentum(_constants.ShatterCost);
			return true;
		}

		public void OnShatterLanded()
		{
			_prematureBlockPenalties = 0;
			Economy.AddMomentumOnHit();
			Economy.ResetFrameAdvantage();
			LastEvent = CombatEvent.ShatterEvent;
		}

		/// <summary>
		/// Shatter whiff: defender was not Parrying when shatter-modified hit landed.
		/// Cost was already paid via TryInitiateShatter (3.0 Momentum).
		/// The only additional punishment is the recovery frame penalty.
		/// </summary>
		public void OnShatterWhiff()
		{
			_prematureBlockPenalties++;
			_shatterWhiffRecoveryPending = true;
			LastEvent = CombatEvent.ShatterWhiff;
		}

		// ── Helpers ────────────────────────────────────────────────────

		private object TransitionToCoil(AttackTier tier)
		{
			var move = _archetype.GetMoveData(tier);
			// ConsumeFrameAdvantageCoilReduction only resets stacks if threshold was met.
			// Attacking without enough stacks preserves them.
			int reduction = Economy.ConsumeFrameAdvantageCoilReduction();
			int effective = Math.Max(1, move.CoilFrames - reduction);
			CurrentTier = tier;
			return new Coil(tier, effective);
		}

		private bool IsMovingToward(PlayerInput input)
		{
			var dx = input.OpponentPosX - PosX;
			var dz = input.OpponentPosZ - PosZ;
			var dot = input.MoveX * dx + input.MoveZ * dz;
			// Require meaningful forward component to prevent strafing exploit.
			// Dot product of two unit vectors: 0.5 ≈ 60° cone toward opponent.
			var distSq = dx * dx + dz * dz;
			if (distSq <= Fixed64.Zero) return false;
			// dot / sqrt(distSq) gives the cosine of the angle (moveDir is already normalized).
			// threshold² * distSq avoids the sqrt: dot² > threshold² * distSq
			var threshold = (Fixed64)0.5;
			return dot > Fixed64.Zero && dot * dot > threshold * threshold * distSq;
		}

		public void ResetState()
		{
			_state = new Idle();
			_blockPressedFramesAgo = int.MaxValue / 2;
			_shatterWhiffRecoveryPending = false;
			_prematureBlockPenalties = 0;
			_parrySucceededThisAttempt = false;
			_posInitialized = false;
			_buffer.Clear();
		}
	}
}

