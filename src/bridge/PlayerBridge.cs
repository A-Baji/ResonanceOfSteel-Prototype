// The Godot-facing conduit layer. Polls input, calls simulation Tick(), applies movement.
// This IS allowed to use Godot types. It must NOT contain combat logic.
using Godot;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;
using ResonanceOfSteel.Bridge.Archetypes;

namespace ResonanceOfSteel.Bridge
{
	public sealed partial class PlayerBridge : CharacterBody3D
	{
		// ── Inspector-adjustable economy constants (Brief Section 11.2) ───
		[ExportGroup("Economy")]
		[Export] public float MomentumMax = 8.0f;
		[Export] public float PerfectParryCost = 0.5f;
		[Export] public float DodgeCost = 1.5f;
		[Export] public float JumpCost = 1.0f;
		[Export] public float ShatterCost = 3.0f;
		[Export] public float BaseMomentumOnHit = 0.5f;
		[Export] public float WalkMomentumRate = 0.05f;
		[Export] public float RunMomentumRate = 0.1f;
		[Export] public float ClashMomentumSurge = 2.0f;
		[Export] public float ComposureRecoveryRate = 0.0036f;
		[Export] public int ComposureRecoveryCooldownFrames = 90;
		[Export] public float TerminalVitalityThreshold = 0.1f;
		[Export] public float BaseVitalityDamage = 0.08f;
		[Export] public float BaseComposureDamage = 0.06f;
		[Export] public float ChipDamageMultiplier = 0.2f;
		[Export] public float ArmorTradeLethality = 1.5f;
		[Export] public float StaggerKnockbackMultiplier = 0.2f;
		[Export] public int FrameAdvantageOffset = 1;
		[Export] public int StackDecayDelayFrames = 180;
		[Export] public int StackDecayIntervalFrames = 60;

		[ExportGroup("Frame Constants")]
		[Export] public int ParryWindowFrames = 6;
		[Export] public int DodgeStartupFrames = 3;
		[Export] public int DodgeActiveFrames = 12;
		[Export] public int DodgeRecoveryFrames = 3;
		[Export] public int JumpStartupFrames = 3;
		[Export] public int JumpActiveFrames = 22;
		[Export] public int JumpRecoveryFrames = 5;
		[Export] public int EvasionFatigueStartupPenalty = 4;
		[Export] public int EvasionFatigueActiveReduction = 4;
		[Export] public int ClashRecoveryFrames = 8;
		[Export] public int ShatterWhiffPenaltyFrames = 20;
		[Export] public int InputBufferTTL = 6;

		[ExportGroup("Setup")]
		[Export] public ArchetypeType Archetype = ArchetypeType.Longsword;
		[Export] public HitboxManager ActiveHitboxManager;
		[Export] public float WalkSpeed = 4.0f;
		[Export] public float RunSpeed = 7.0f;
		[Export] public float BlockWalkSpeed = 2.0f;
		[Export] public Node3D CameraPivot;
		[Export] public int PlayerIndex = 0;
		[Export] public float BoundaryPushbackStrength = 5.0f;

		// ── Signals emitted to the Presentation Layer ───────────────────
		[Signal] public delegate void HitLandedEventHandler();
		[Signal] public delegate void HitBlockedEventHandler();
		[Signal] public delegate void ParrySuccessEventHandler();
		[Signal] public delegate void ShatterEventHandler();
		[Signal] public delegate void ShatterWhiffEventHandler();
		[Signal] public delegate void ClashEventHandler();
		[Signal] public delegate void FatigueEnteredEventHandler();
		[Signal] public delegate void FatigueExitedEventHandler();
		[Signal] public delegate void DeathblowTriggeredEventHandler();

		// ── Internal references ─────────────────────────────────────────
		private PlayerSimulation _sim;
		private bool _wasFatigued;
		private bool _actionNamesCached;

		// Cached action name strings to avoid per-frame string interpolation.
		private string _moveLeft, _moveRight, _moveUp, _moveDown;
		private string _attack, _blockParry, _dodge, _jump, _run;
		private string _modLight, _modHeavy, _modSuper;

		// Converts integer KnockbackDistance from MoveData to world-space displacement.
		private const float KnockbackScale = 0.1f;

		// When true, _PhysicsProcess is a no-op; GameCoordinator drives the phases.
		private bool _coordinatorDriven;

		// Pending knockback velocity for smooth application over multiple frames.
		private Vector3 _knockbackVelocity;

		public PlayerBridge Opponent { get; set; }
		public IArchetypeData ArchetypeData { get; private set; }
		public IArchetypeVisuals ArchetypeVisuals { get; private set; }

		public override void _Ready()
		{
			InitializeArchetype();
			_sim = new PlayerSimulation(BuildConstants(), ArchetypeData);

			if (ActiveHitboxManager != null)
			{
				ActiveHitboxManager.ArchetypeVisuals = ArchetypeVisuals;
				ActiveHitboxManager.ArchetypeData = ArchetypeData;
			}
			else
			{
				GD.PushWarning($"ActiveHitboxManager is missing on {Name}!");
			}
		}

		private void InitializeArchetype()
		{
			if (Archetype == ArchetypeType.Greatsword)
			{
				ArchetypeData = GreatswordData.Instance;
				ArchetypeVisuals = GreatswordVisuals.Instance;
			}
			else
			{
				ArchetypeData = LongswordData.Instance;
				ArchetypeVisuals = LongswordVisuals.Instance;
			}
		}

		private void CacheActionNames()
		{
			var p = PlayerIndex == 0 ? "" : "_p2";
			_moveLeft = $"move_left{p}";
			_moveRight = $"move_right{p}";
			_moveUp = $"move_up{p}";
			_moveDown = $"move_down{p}";
			_attack = $"attack{p}";
			_blockParry = $"block_parry{p}";
			_dodge = $"dodge{p}";
			_jump = $"jump{p}";
			_run = $"run{p}";
			_modLight = $"modifier_light{p}";
			_modHeavy = $"modifier_heavy{p}";
			_modSuper = $"modifier_super{p}";
		}

		/// <summary>
		/// Called by GameCoordinator to disable independent _PhysicsProcess.
		/// When coordinator-driven, TickPhase/ResolvePhase are called explicitly.
		/// </summary>
		public void SetCoordinatorDriven() => _coordinatorDriven = true;

		public override void _PhysicsProcess(double delta)
		{
			if (_coordinatorDriven) return;

			// Fallback for standalone testing without a coordinator.
			TickPhase(delta);
			ResolvePhase();
		}

		/// <summary>
		/// Phase 1: Sample input, advance simulation, apply movement.
		/// Both players complete this phase before any hitbox resolution.
		/// </summary>
		public void TickPhase(double delta)
		{
			if (!_actionNamesCached)
			{
				CacheActionNames();
				_actionNamesCached = true;
			}

			var input = BuildPlayerInput();
			_sim.Tick(input);

			ApplyMovement(input, delta);
			FaceOpponent();
			ApplyBoundaryPushback();
		}

		/// <summary>
		/// Phase 2: Hitbox queries and combat event emission.
		/// Runs after both players have ticked, ensuring symmetric state.
		/// </summary>
		public void ResolvePhase()
		{
			ActiveHitboxManager?.ProcessHitboxes();
			EmitCombatEvents();
		}

		// ── Input sampling ──────────────────────────────────────────────
		// Raw hardware state — the Simulation's InputBuffer handles buffering/consumption.
		private PlayerInput BuildPlayerInput()
		{
			// Raw stick axes — camera-relative, not world-space.
			var rawMx = Input.GetAxis(_moveLeft, _moveRight);
			var rawMz = Input.GetAxis(_moveUp, _moveDown);

			// Transform to world-space direction so IsMovingToward (RoW, dodge) works correctly.
			float worldMoveX = 0f, worldMoveZ = 0f;
			if (!Mathf.IsZeroApprox(rawMx) || !Mathf.IsZeroApprox(rawMz))
			{
				var basis = CameraPivot != null
					? CameraPivot.GlobalTransform.Basis
					: Basis.Identity;
				var camForward = new Vector3(-basis.Z.X, 0, -basis.Z.Z).Normalized();
				var camRight = new Vector3(basis.X.X, 0, basis.X.Z).Normalized();
				var moveDir = (camForward * -rawMz + camRight * rawMx).Normalized();
				worldMoveX = moveDir.X;
				worldMoveZ = moveDir.Z;
			}

			var mx = (Fixed64)(double)worldMoveX;
			var mz = (Fixed64)(double)worldMoveZ;

			var tier = AttackTier.Standard;
			if (Input.IsActionPressed(_modLight)) tier = AttackTier.Light;
			if (Input.IsActionPressed(_modHeavy)) tier = AttackTier.Heavy;
			if (Input.IsActionPressed(_modSuper)) tier = AttackTier.Super;

			var ownX = (Fixed64)(double)GlobalPosition.X;
			var ownZ = (Fixed64)(double)GlobalPosition.Z;
			var oppX = Opponent != null ? (Fixed64)(double)Opponent.GlobalPosition.X : Fixed64.Zero;
			var oppZ = Opponent != null ? (Fixed64)(double)Opponent.GlobalPosition.Z : Fixed64.Zero;

			return new PlayerInput(
				moveX: mx,
				moveZ: mz,
				runHeld: Input.IsActionPressed(_run),
				attackJustPressed: Input.IsActionJustPressed(_attack),
				blockParryHeld: Input.IsActionPressed(_blockParry),
				blockParryJustPressed: Input.IsActionJustPressed(_blockParry),
				dodgeJustPressed: Input.IsActionJustPressed(_dodge),
				jumpJustPressed: Input.IsActionJustPressed(_jump),
				modifierTier: tier,
				isGrounded: IsOnFloor(),
				ownPosX: ownX,
				ownPosZ: ownZ,
				opponentPosX: oppX,
				opponentPosZ: oppZ
			);
		}

		// ── Movement application ────────────────────────────────────────
		// Friction factor for slide-to-stop during committed actions.
		private const float ActionLockFriction = 0.95f;

		private void ApplyMovement(PlayerInput input, double delta)
		{
			bool knockbackActive = _knockbackVelocity.LengthSquared() > 1.0f;

			if (_sim.IsInDeathblow || _sim.IsStaggered)
			{
				// Deathblow/Staggered: complete halt (knockback still applies via overlay).
				Velocity = Vector3.Zero;
			}
			else if (_sim.IsActionLocked)
			{
				// Committed action: slide to stop from prior velocity.
				var slide = new Vector3(Velocity.X, 0, Velocity.Z) * ActionLockFriction;
				Velocity = slide.LengthSquared() < 0.01f ? Vector3.Zero : slide;
			}
			else if (knockbackActive)
			{
				// Active knockback during non-locked state: suppress input movement.
				Velocity = Vector3.Zero;
			}
			else if (input.HasMovement)
			{
				// Blocking suppresses running and limits speed to BlockWalkSpeed.
				float speed = _sim.IsBlocking
					? BlockWalkSpeed
					: (input.RunHeld ? RunSpeed : WalkSpeed);
				var moveDir = new Vector3((float)input.MoveX, 0, (float)input.MoveZ);
				Velocity = moveDir * speed;
			}
			else
			{
				Velocity = Vector3.Zero;
			}

			// Blend in pending knockback velocity and decay it.
			if (_knockbackVelocity.LengthSquared() > 0.01f)
			{
				Velocity += _knockbackVelocity;
				_knockbackVelocity *= 0.75f;
			}
			else
			{
				_knockbackVelocity = Vector3.Zero;
			}

			MoveAndSlide();
		}

		private void ApplyBoundaryPushback()
		{
			for (int i = 0; i < GetSlideCollisionCount(); i++)
			{
				var collision = GetSlideCollision(i);
				if (collision.GetCollider() is Node collider && collider.IsInGroup("boundary"))
				{
					Velocity += collision.GetNormal() * BoundaryPushbackStrength;
					MoveAndSlide();
					break;
				}
			}
		}

		private void FaceOpponent()
		{
			if (Opponent == null) return;

			var toOpponent = Opponent.GlobalPosition - GlobalPosition;
			toOpponent.Y = 0;
			if (toOpponent.LengthSquared() < 0.001f) return;

			float targetYaw = Mathf.Atan2(-toOpponent.X, -toOpponent.Z);
			float current = GlobalRotation.Y;
			float diff = Mathf.AngleDifference(current, targetYaw);

			GlobalRotation = GlobalRotation with { Y = current + diff };
		}

		// ── Event emission ──────────────────────────────────────────────
		private void EmitCombatEvents()
		{
			switch (_sim.LastEvent)
			{
				case CombatEvent.HitLand: EmitSignal(SignalName.HitLanded); break;
				case CombatEvent.HitBlocked: EmitSignal(SignalName.HitBlocked); break;
				case CombatEvent.ParrySuccess: EmitSignal(SignalName.ParrySuccess); break;
				case CombatEvent.ShatterEvent: EmitSignal(SignalName.Shatter); break;
				case CombatEvent.ShatterWhiff: EmitSignal(SignalName.ShatterWhiff); break;
				case CombatEvent.ClashEvent: EmitSignal(SignalName.Clash); break;
				case CombatEvent.DeathblowTriggered: EmitSignal(SignalName.DeathblowTriggered); break;
			}

			bool isFatigued = _sim.Economy.IsFatigued;
			if (isFatigued && !_wasFatigued) EmitSignal(SignalName.FatigueEntered);
			if (!isFatigued && _wasFatigued) EmitSignal(SignalName.FatigueExited);
			_wasFatigued = isFatigued;
		}

		// ── Public accessors (read by Presentation/HUD/HitboxManager) ───
		public float GetVitality() => (float)_sim.Economy.Vitality;
		public float GetComposure() => (float)_sim.Economy.Composure;
		public float GetMomentum() => (float)_sim.Economy.Momentum;
		public int GetFrameAdvantageStacks() => _sim.Economy.FrameAdvantageStacks;
		public bool IsHitboxActive() => _sim.HitboxActive;
		public bool IsArmorActive() => _sim.ArmorActive;
		public AttackTier GetCurrentTier() => _sim.CurrentTier;
		public Fixed64 GetArmorTradeLethality() => (Fixed64)(double)ArmorTradeLethality;

		// ── Semantic state queries (replace string-based checks) ────────
		public bool IsBlocking() => _sim.IsBlocking;
		public bool IsParrying() => _sim.IsParrying;
		public bool IsInDeathblow() => _sim.IsInDeathblow;

		// ── Debug accessors (read by DebugHUD) ─────────────────────────
		public string GetDebugStateName() => _sim.DebugStateName;
		public int GetDebugBufferCount() => _sim.DebugBufferCount;
		public bool GetIsFatigued() => _sim.Economy.IsFatigued;
		public bool GetIsTerminal() => _sim.Economy.IsTerminal;
		public bool GetIsDeathblowVulnerable() => _sim.Economy.IsDeathblowVulnerable;
		public bool GetIsActionLocked() => _sim.IsActionLocked;
		public bool GetIsArmorActive() => _sim.ArmorActive;
		public string GetArchetypeName() => Archetype.ToString();
		public string GetLastEventName() => _sim.LastEvent.ToString();

		// ── Shatter / Clash API ─────────────────────────────────────────
		public bool IsInShatterWindow() => _sim.IsInShatterWindow;
		public bool CanAffordShatter() => _sim.Economy.CanAfford((Fixed64)(double)ShatterCost);
		public bool TryInitiateShatter() => _sim.TryInitiateShatter();
		public bool IsClashedThisFrame() => _sim.ClashedThisFrame;

		// ── Combat event notifications (called by HitboxManager) ────────
		public void ReceiveHit(Fixed64 vMult, Fixed64 cMult, bool blocked,
			AttackTier attackerTier, int staggerFrames)
			=> _sim.OnHitReceived(vMult, cMult, blocked, attackerTier, staggerFrames);

		public void NotifyHitLanded(Fixed64 vMult, Fixed64 cMult, bool blocked)
			=> _sim.OnHitLanded(vMult, cMult, blocked);

		public void NotifyParrySuccess() => _sim.OnParrySuccess();
		public void NotifyDeathblowTriggered() => _sim.OnDeathblowTriggered();
		public void NotifyShatterLanded() => _sim.OnShatterLanded();
		public void NotifyShatterWhiff() => _sim.OnShatterWhiff();
		public void NotifyClash() => _sim.OnClash();

		public void ApplyKnockback(Vector3 direction, float distance)
		{
			// Convert displacement to velocity impulse. Decays over several frames in ApplyMovement.
			_knockbackVelocity += direction * distance * KnockbackScale * 60f;
		}

		// ── Reset (called between rounds) ───────────────────────────────
		public void FullReset(Vector3 spawnPosition)
		{
			_sim.Economy.FullReset();
			_sim.ResetState();
			GlobalPosition = spawnPosition;
			Velocity = Vector3.Zero;
			_knockbackVelocity = Vector3.Zero;
			_wasFatigued = false;
		}

		// ── Builds EconomyConstants from exported inspector values ───────
		private EconomyConstants BuildConstants() => new(
			MomentumMax: (Fixed64)(double)MomentumMax,
			PerfectParryCost: (Fixed64)(double)PerfectParryCost,
			DodgeCost: (Fixed64)(double)DodgeCost,
			JumpCost: (Fixed64)(double)JumpCost,
			ShatterCost: (Fixed64)(double)ShatterCost,
			BaseMomentumOnHit: (Fixed64)(double)BaseMomentumOnHit,
			WalkMomentumRate: (Fixed64)(double)WalkMomentumRate,
			RunMomentumRate: (Fixed64)(double)RunMomentumRate,
			ClashMomentumSurge: (Fixed64)(double)ClashMomentumSurge,
			ComposureBaseRecoveryRate: (Fixed64)(double)ComposureRecoveryRate,
			ComposureRecoveryCooldownFrames: ComposureRecoveryCooldownFrames,
			TerminalVitalityThreshold: (Fixed64)(double)TerminalVitalityThreshold,
			BaseVitalityDamage: (Fixed64)(double)BaseVitalityDamage,
			BaseComposureDamage: (Fixed64)(double)BaseComposureDamage,
			ChipDamageMultiplier: (Fixed64)(double)ChipDamageMultiplier,
			FrameAdvantageOffset: FrameAdvantageOffset,
			StackDecayDelayFrames: StackDecayDelayFrames,
			StackDecayIntervalFrames: StackDecayIntervalFrames,
			ArmorTradeLethality: (Fixed64)(double)ArmorTradeLethality,
			StaggerKnockbackMultiplier: (Fixed64)(double)StaggerKnockbackMultiplier,
			ParryWindowFrames: ParryWindowFrames,
			DodgeStartupFrames: DodgeStartupFrames,
			DodgeActiveFrames: DodgeActiveFrames,
			DodgeRecoveryFrames: DodgeRecoveryFrames,
			JumpStartupFrames: JumpStartupFrames,
			JumpActiveFrames: JumpActiveFrames,
			JumpRecoveryFrames: JumpRecoveryFrames,
			EvasionFatigueStartupPenalty: EvasionFatigueStartupPenalty,
			EvasionFatigueActiveReduction: EvasionFatigueActiveReduction,
			ClashRecoveryFrames: ClashRecoveryFrames,
			ShatterWhiffPenaltyFrames: ShatterWhiffPenaltyFrames,
			InputBufferTTL: InputBufferTTL
		);
	}
}
