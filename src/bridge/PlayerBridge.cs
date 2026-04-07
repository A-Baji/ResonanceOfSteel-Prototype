
// The Godot-facing layer. Polls input, calls simulation Tick(), applies movement.
// This IS allowed to use Godot types. It must NOT contain combat logic.
using Godot;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;
using ResonanceOfSteel.Bridge.Archetypes;

namespace ResonanceOfSteel.Bridge
{
	public partial class PlayerBridge : CharacterBody3D
	{
		// ── Inspector-adjustable economy constants (Brief Section 11.2) ───
		[Export] public float BaseMomentumOnHit = 0.5f;
		[Export] public float WalkMomentumRate = 0.05f;
		[Export] public float RunMomentumRate = 0.1f;
		[Export] public float ClashMomentumSurge = 2.0f;
		[Export] public float ComposureRecoveryRate = 0.02f;
		[Export] public float TerminalVitalityThreshold = 0.1f;
		[Export] public float BaseVitalityDamage = 0.08f;
		[Export] public float BaseComposureDamage = 0.06f;
		[Export] public float ArmorTradeLethality = 1.5f;
		[Export] public int FrameAdvantageThreshold = 3;
		[Export] public int FrameAdvantageOffset = 3;
		[Export] public int ShatterContactWindow = 8;
		[Export] public ArchetypeType Archetype = ArchetypeType.Longsword;
		[Export] public HitboxManager ActiveHitboxManager;
		// ── Movement constants ──────────────────────────────────────────
		[Export] public float WalkSpeed = 4.0f;
		[Export] public float RunSpeed = 7.0f;

		// ── Which player this bridge controls ───────────────────────────
		// 0 = Player 1 (uses default action names)
		// 1 = Player 2 (uses _p2 suffix action names, added in Phase 7)
		[Export] public int PlayerIndex = 0;

		// ── Signals emitted to the Presentation Layer ───────────────────
		[Signal] public delegate void HitLandedEventHandler();
		[Signal] public delegate void HitBlockedEventHandler();
		[Signal] public delegate void ParrySuccessEventHandler();
		[Signal] public delegate void ShatterEventHandler();
		[Signal] public delegate void ClashEventHandler();
		[Signal] public delegate void FatigueEnteredEventHandler();
		[Signal] public delegate void FatigueExitedEventHandler();
		[Signal] public delegate void DeathblowTriggeredEventHandler();

		// ── Internal references ─────────────────────────────────────────
		private PlayerSimulation _sim;
		private InputBuffer _buffer;
		private bool _wasFatigued;

		// The opponent bridge (set externally by the match manager in Phase 9).
		public PlayerBridge Opponent { get; set; }

		// Expose these so HitboxManager can grab them during _Ready or from the Coordinator
		public IArchetypeData ArchetypeData { get; private set; }
		public IArchetypeVisuals ArchetypeVisuals { get; private set; }

		public override void _Ready()
		{
			// 1. Instantiate the polymorphic archetype classes
			InitializeArchetype();

			// 2. Initialize simulation and input buffer
			var constants = BuildConstants();
			_sim = new PlayerSimulation(constants, ArchetypeData);
			_buffer = new InputBuffer();

			// 3. Inject dependencies into the HitboxManager (if linked in inspector/coordinator)
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
				ArchetypeData = new GreatswordData();
				ArchetypeVisuals = new GreatswordVisuals();
			}
			else
			{
				ArchetypeData = new LongswordData();
				ArchetypeVisuals = new LongswordVisuals();
			}
		}

		public override void _PhysicsProcess(double delta)
		{
			// 1. Tick the input buffer (decrement TTLs).
			_buffer.Tick();

			// 2. Poll hardware input and add new presses to buffer.
			PollHardwareInput();

			// 3. Build this frame's PlayerInput.
			var input = BuildPlayerInput();

			// 4. Calculate opponent position (restored missing logic)
			var oppX = Opponent != null ? (Fixed64)(double)Opponent.GlobalPosition.X : (Fixed64)0;
			var oppZ = Opponent != null ? (Fixed64)(double)Opponent.GlobalPosition.Z : (Fixed64)0;

			// 5. Tick the simulation.
			_sim.Tick(input, oppX, oppZ);

			// 6. Apply movement from simulation state.
			ApplyMovement(input, delta);

			// 7. DETERMINISTIC EXECUTION: Command the hitbox check right now
			if (ActiveHitboxManager != null)
			{
				ActiveHitboxManager.ProcessHitboxes();
			}

			// 8. Emit signals based on events from the simulation.
			EmitCombatEvents();
		}
		// ── Input polling ───────────────────────────────────────────────
		private void PollHardwareInput()
		{
			var p = PlayerIndex == 0 ? "" : "_p2";

			if (Input.IsActionJustPressed($"attack{p}")) _buffer.Add(PlayerInputAction.Attack);
			if (Input.IsActionJustPressed($"block_parry{p}")) _buffer.Add(PlayerInputAction.BlockParry);
			if (Input.IsActionJustPressed($"dodge{p}")) _buffer.Add(PlayerInputAction.Dodge);
			if (Input.IsActionJustPressed($"jump{p}")) _buffer.Add(PlayerInputAction.Jump);
		}

		private PlayerInput BuildPlayerInput()
		{
			var p = PlayerIndex == 0 ? "" : "_p2";

			var mx = (Fixed64)(double)(Input.GetAxis($"move_left{p}", $"move_right{p}"));
			var mz = (Fixed64)(double)(Input.GetAxis($"move_up{p}", $"move_down{p}"));
			var runHeld = Input.IsActionPressed($"run{p}");

			// Determine modifier tier from held buttons at moment of attack.
			// Only matters when Attack is the consumed input.
			var tier = AttackTier.Standard; // Default: no modifier = Standard (Tier 1)
			if (Input.IsActionPressed($"modifier_light{p}")) tier = AttackTier.Light;
			if (Input.IsActionPressed($"modifier_heavy{p}")) tier = AttackTier.Heavy;
			if (Input.IsActionPressed($"modifier_super{p}")) tier = AttackTier.Super;

			// Consume the highest-priority buffered input for this frame.
			var consumed = _buffer.Consume();

			return new PlayerInput(
				MoveX: mx,
				MoveZ: mz,
				RunHeld: runHeld,
				AttackPressed: consumed == PlayerInputAction.Attack,
				BlockParryPressed: consumed == PlayerInputAction.BlockParry
								|| Input.IsActionPressed($"block_parry{p}"),
				DodgePressed: consumed == PlayerInputAction.Dodge,
				JumpPressed: consumed == PlayerInputAction.Jump,
				ModifierTier: tier
			);
		}

		// ── Movement application ────────────────────────────────────────
		private void ApplyMovement(PlayerInput input, double delta)
		{
			// Simple movement: move in the direction of left stick.
			// Rotation to face movement direction.
			if (input.MoveX != Fixed64.Zero || input.MoveZ != Fixed64.Zero)
			{
				float speed = (bool)input.RunHeld ? RunSpeed : WalkSpeed;
				var vel = new Vector3((float)input.MoveX, 0, (float)input.MoveZ).Normalized() * speed;
				Velocity = vel;

				// Face movement direction.
				var lookDir = new Vector3((float)input.MoveX, 0, (float)input.MoveZ);
				if (lookDir.LengthSquared() > 0)
					Rotation = new Vector3(0, Mathf.Atan2(-lookDir.X, -lookDir.Z), 0);
			}
			else
			{
				Velocity = Vector3.Zero;
			}

			MoveAndSlide();
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
				case CombatEvent.ClashEvent: EmitSignal(SignalName.Clash); break;
				case CombatEvent.DeathblowTriggered: EmitSignal(SignalName.DeathblowTriggered); break;
			}

			bool isFatigued = _sim.Economy.IsFatigued;
			if (isFatigued && !_wasFatigued) EmitSignal(SignalName.FatigueEntered);
			if (!isFatigued && _wasFatigued) EmitSignal(SignalName.FatigueExited);
			_wasFatigued = isFatigued;
		}

		// ── Public accessors (read by Presentation and HUD) ─────────────
		public float GetVitality() => (float)_sim.Economy.Vitality;
		public float GetComposure() => (float)_sim.Economy.Composure;
		public float GetMomentum() => (float)_sim.Economy.Momentum;
		public int GetFrameAdvantageStacks() => _sim.Economy.FrameAdvantageStacks;
		public string GetStateName() => _sim.GetStateName();
		public bool IsHitboxActive() => _sim.HitboxActive;
		public bool IsArmorActive() => _sim.ArmorActive;
		public AttackTier GetCurrentTier() => _sim.CurrentTier;

		// Called by the HitboxManager in Phase 5 when a hit is resolved.
		public void ReceiveHit(Fixed64 vMult, Fixed64 cMult, bool blocked)
		{
			_sim.OnHitReceived(vMult, cMult, blocked);
		}

		// Called by the HitboxManager when this player's hit landed.
		public void NotifyHitLanded(Fixed64 vMult, Fixed64 cMult, bool blocked)
		{
			_sim.OnHitLanded(vMult, cMult, blocked);
		}

		public void NotifyParrySuccess()
		{
			_sim.OnParrySuccess();
			EmitSignal(SignalName.ParrySuccess);
		}

		// ── Reset (called between rounds) ───────────────────────────────
		public void FullReset(Vector3 spawnPosition)
		{
			_sim.Economy.FullReset();
			_buffer.Clear();
			GlobalPosition = spawnPosition;
			Velocity = Vector3.Zero;
			_wasFatigued = false;
		}

		// ── Builds EconomyConstants from exported inspector values ───────
		private EconomyConstants BuildConstants() => new(
			MomentumMax: (Fixed64)8.0,
			PerfectParryCost: (Fixed64)0.5,
			DodgeCost: (Fixed64)1.5,
			ShatterCost: (Fixed64)3.0,
			BaseMomentumOnHit: (Fixed64)(double)BaseMomentumOnHit,
			WalkMomentumRate: (Fixed64)(double)WalkMomentumRate,
			RunMomentumRate: (Fixed64)(double)RunMomentumRate,
			ClashMomentumSurge: (Fixed64)(double)ClashMomentumSurge,
			ComposureBaseRecoveryRate: (Fixed64)(double)ComposureRecoveryRate,
			TerminalVitalityThreshold: (Fixed64)(double)TerminalVitalityThreshold,
			BaseVitalityDamage: (Fixed64)(double)BaseVitalityDamage,
			BaseComposureDamage: (Fixed64)(double)BaseComposureDamage,
			FrameAdvantageThreshold: FrameAdvantageThreshold,
			FrameAdvantageOffset: FrameAdvantageOffset,
			ShatterContactWindow: ShatterContactWindow,
			ArmorTradeLethality: (Fixed64)(double)ArmorTradeLethality
		);
	}
}

