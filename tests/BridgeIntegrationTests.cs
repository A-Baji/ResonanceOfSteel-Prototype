// Bridge-layer integration tests using GdUnit4 SceneRunner.
// Requires Godot runtime — run with `dotnet test` on a machine with Godot installed.
// Tests load Game.tscn for full integration (GameCoordinator, two Players, Stage).
//
// These tests cover bridge behaviors that pure simulation tests CANNOT reach:
//   - Block-walk speed capping (PlayerBridge.ApplyMovement)
//   - Run suppression while blocking
//   - Two-pass tick/resolve architecture (GameCoordinator)
//   - Combat signal emission (Bridge → Presentation)
//   - Export → EconomyConstants wiring
//   - HitboxManager + CombatResolver integration
using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using ResonanceOfSteel.Bridge;
using ResonanceOfSteel.Scene;
using System.Threading.Tasks;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	[RequireGodotRuntime]
	public partial class BridgeIntegrationTests
	{
		private const string GameScene = "res://scenes/Game.tscn";

		// ══════════════════════════════════════════════════════════════
		//  Scene initialization
		// ══════════════════════════════════════════════════════════════

		/// <summary>Game scene loads without error and has both players.</summary>
		[TestCase]
		public async Task Scene_Loads_Both_Players()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			var p2 = runner.FindChild("Player2") as PlayerBridge;
			AssertThat(p1).IsNotNull();
			AssertThat(p2).IsNotNull();
		}

		/// <summary>Both players start in Idle state after scene load.</summary>
		[TestCase]
		public async Task Players_Start_Idle()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			var p2 = runner.FindChild("Player2") as PlayerBridge;
			AssertThat(p1!.GetDebugStateName()).IsEqual("Idle");
			AssertThat(p2!.GetDebugStateName()).IsEqual("Idle");
		}

		/// <summary>§8.1: P1 defaults to Longsword, P2 to Greatsword (scene export).</summary>
		[TestCase]
		public async Task Archetype_Assignment_From_Scene()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			var p2 = runner.FindChild("Player2") as PlayerBridge;
			AssertThat(p1!.GetArchetypeName()).IsEqual("Longsword");
			AssertThat(p2!.GetArchetypeName()).IsEqual("Greatsword");
		}

		/// <summary>§4.1: Initial momentum = half max (4.0).</summary>
		[TestCase]
		public async Task Initial_Economy_Values()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			AssertThat((double)p1!.GetMomentum()).IsEqualApprox(4.0, 0.01);
			AssertThat((double)p1.GetVitality()).IsEqualApprox(1.0, 0.01);
			AssertThat((double)p1.GetComposure()).IsEqualApprox(0.0, 0.01);
			AssertThat(p1.GetFrameAdvantageStacks()).IsEqual(0);
		}

		// ══════════════════════════════════════════════════════════════
		//  Block-walk speed capping (new feature)
		// ══════════════════════════════════════════════════════════════

		/// <summary>
		/// Blocking + movement → speed capped at BlockWalkSpeed (2.0).
		/// Without block, WalkSpeed is 4.0. The velocity magnitude should
		/// be noticeably lower when blocking.
		/// </summary>
		[TestCase]
		public async Task BlockWalk_Speed_Lower_Than_Normal_Walk()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			// Measure normal walk velocity
			runner.SimulateActionPress("move_up");
			await runner.SimulateFrames(3);
			var p1 = runner.FindChild("Player1") as PlayerBridge;
			float normalSpeed = new Vector2(p1!.Velocity.X, p1.Velocity.Z).Length();
			runner.SimulateActionRelease("move_up");
			await runner.SimulateFrames(2);

			// Measure block-walk velocity
			runner.SimulateActionPress("block_parry");
			await runner.AwaitInputProcessed();
			runner.SimulateActionPress("move_up");
			await runner.SimulateFrames(3);
			float blockSpeed = new Vector2(p1.Velocity.X, p1.Velocity.Z).Length();
			runner.SimulateActionRelease("block_parry");
			runner.SimulateActionRelease("move_up");

			// Block-walk speed (2.0) should be less than normal walk speed (4.0)
			AssertThat((double)blockSpeed).IsLess((double)normalSpeed);
		}

		/// <summary>
		/// Blocking + run held → run is suppressed, speed matches BlockWalkSpeed.
		/// </summary>
		[TestCase]
		public async Task BlockWalk_Suppresses_Run()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);
			var p1 = runner.FindChild("Player1") as PlayerBridge;

			// ── Baseline: measure actual run speed (no block) ─────────
			runner.SimulateActionPress("run");
			runner.SimulateActionPress("move_up");
			await runner.SimulateFrames(5);
			float runSpeed = new Vector2(p1!.Velocity.X, p1.Velocity.Z).Length();
			runner.SimulateActionRelease("run");
			runner.SimulateActionRelease("move_up");
			await runner.SimulateFrames(5);

			// Sanity: run should be near RunSpeed (7.0)
			AssertThat((double)runSpeed).IsGreater(5.0);

			// ── Test: block + run + move ───────────────────────────────
			// We do NOT assert intermediate state here. GdUnit4 re-injects
			// block_parry as JustPressed on every physics frame inside
			// SimulateFrames, causing the player to cycle:
			// Idle → Parrying(window) → Blocking (1 frame) → repeat
			// with accumulating penalties. During Parrying the player is
			// action-locked (slide-to-stop, speed→0). During Blocking the
			// player is capped at BlockWalkSpeed (2.0). In both cases,
			// speed is far below RunSpeed (7.0), which is what we want to
			// verify: run held while blocking is suppressed.
			runner.SimulateActionPress("block_parry");
			runner.SimulateActionPress("run");
			runner.SimulateActionPress("move_up");
			await runner.SimulateFrames(10);
			float blockRunSpeed = new Vector2(p1.Velocity.X, p1.Velocity.Z).Length();

			// block+run speed must be substantially less than vanilla run speed.
			// With suppression: blockRunSpeed ≤ 2.5 (block-walk or slide-to-stop).
			// Without suppression: blockRunSpeed ≈ 7.0 (would fail this check).
			AssertThat((double)blockRunSpeed).IsLess((double)runSpeed * 0.5);

			runner.SimulateActionRelease("block_parry");
			runner.SimulateActionRelease("run");
			runner.SimulateActionRelease("move_up");
		}
		// ══════════════════════════════════════════════════════════════
		//  Two-pass tick/resolve architecture (§10.2)
		// ══════════════════════════════════════════════════════════════

		/// <summary>
		/// GameCoordinator drives both players with SetCoordinatorDriven.
		/// After processing, both players should have consistent states.
		/// </summary>
		[TestCase]
		public async Task Coordinator_Drives_Both_Players()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(5);

			// Both players should be alive and ticking
			var p1 = runner.FindChild("Player1") as PlayerBridge;
			var p2 = runner.FindChild("Player2") as PlayerBridge;
			AssertThat(p1!.GetDebugStateName()).IsNotEqual("Unknown");
			AssertThat(p2!.GetDebugStateName()).IsNotEqual("Unknown");
		}

		// ══════════════════════════════════════════════════════════════
		//  Attack input → state transition through bridge
		// ══════════════════════════════════════════════════════════════

		/// <summary>P1 attack input flows through bridge to simulation Coil state.</summary>
		[TestCase]
		public async Task Attack_Input_Reaches_Simulation()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			runner.SimulateActionPressed("attack");
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			// Should be in Coil after attack press
			AssertThat(p1!.GetDebugStateName()).Contains("Coil");
		}

		/// <summary>Block input reaches simulation and enters Blocking state.</summary>
		[TestCase]
		public async Task Block_Input_Reaches_Simulation()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			runner.SimulateActionPress("block_parry");
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			// Should be in Parrying or Blocking after block_parry press
			bool isBlocking = p1!.IsBlocking();
			bool isParrying = p1.IsParrying();
			AssertThat(isBlocking || isParrying).IsTrue();

			runner.SimulateActionRelease("block_parry");
		}

		// ══════════════════════════════════════════════════════════════
		//  Export → EconomyConstants wiring
		// ══════════════════════════════════════════════════════════════

		/// <summary>
		/// Verify that PlayerBridge's [Export] values match the spec defaults.
		/// These exports feed BuildConstants() which creates EconomyConstants.
		/// </summary>
		[TestCase]
		public async Task Export_Values_Match_Spec_Defaults()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(1);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			// §4.1 / §15
			AssertThat((double)p1!.MomentumMax).IsEqualApprox(8.0, 0.001);
			AssertThat((double)p1.PerfectParryCost).IsEqualApprox(0.5, 0.001);
			AssertThat((double)p1.DodgeCost).IsEqualApprox(1.5, 0.001);
			AssertThat((double)p1.JumpCost).IsEqualApprox(1.0, 0.001);
			AssertThat((double)p1.ShatterCost).IsEqualApprox(3.0, 0.001);
			AssertThat((double)p1.BaseMomentumOnHit).IsEqualApprox(0.5, 0.001);
			AssertThat((double)p1.ChipDamageMultiplier).IsEqualApprox(0.2, 0.001);
			AssertThat((double)p1.ArmorTradeLethality).IsEqualApprox(1.5, 0.001);
			AssertThat(p1.ParryWindowFrames).IsEqual(6);
			AssertThat(p1.InputBufferTTL).IsEqual(6);
			AssertThat(p1.ClashRecoveryFrames).IsEqual(8);
			AssertThat(p1.ShatterWhiffPenaltyFrames).IsEqual(20);
			AssertThat((double)p1.BlockWalkSpeed).IsEqualApprox(2.0, 0.001);
			AssertThat((double)p1.WalkSpeed).IsEqualApprox(4.0, 0.001);
			AssertThat((double)p1.RunSpeed).IsEqualApprox(7.0, 0.001);
		}

		// ══════════════════════════════════════════════════════════════
		//  Slide-to-stop during committed actions (§5.3)
		// ══════════════════════════════════════════════════════════════

		/// <summary>
		/// When entering a committed action from movement, velocity decays
		/// (slide-to-stop) instead of stopping instantly.
		/// </summary>
		[TestCase]
		public async Task Slide_To_Stop_During_Attack()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			// Start moving
			runner.SimulateActionPress("move_up");
			await runner.SimulateFrames(5);

			// Attack WHILE STILL MOVING (keep move_up held).
			// Releasing move_up and pressing attack on the same frame would cause
			// ProcessMoving to see !HasMovement first and return Idle, silently
			// discarding the consumed Attack action. Keeping move_up held ensures
			// HasMovement=true so ProcessActionableInput fires the attack.
			runner.SimulateActionPress("attack");
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			// Should be in Coil AND still have some residual velocity (slide)
			AssertThat(p1!.GetDebugStateName()).Contains("Coil");
			// Velocity should not be exactly zero — decays via ActionLockFriction (0.95×/frame)
			float speed = new Vector2(p1.Velocity.X, p1.Velocity.Z).Length();
			AssertThat((double)speed).IsGreater(0.0);

			runner.SimulateActionRelease("move_up");
			runner.SimulateActionRelease("attack");
		}

		// ══════════════════════════════════════════════════════════════
		//  Combat signal emission
		// ══════════════════════════════════════════════════════════════

		/// <summary>
		/// When a hit lands (via direct API call), the HitLanded signal
		/// should be emitted.
		/// </summary>
		[TestCase]
		public async Task HitLanded_Signal_Emitted()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;

			// Subscribe before triggering — signal must be monitored before emission.
			bool signalReceived = false;
			p1!.HitLanded += () => signalReceived = true;

			// Advance to swing so the test is realistic (attack→swing→hit).
			runner.SimulateActionPressed("attack");
			await runner.SimulateFrames(10); // past Coil into Swing

			// Use bridge API to notify a hit, then call ResolvePhase() directly.
			// IMPORTANT: calling SimulateFrames() after NotifyHitLanded would run
			// Tick() which resets LastEvent = None before EmitCombatEvents can read
			// it. All other signal tests use ResolvePhase() for the same reason.
			p1.NotifyHitLanded(
				(FixedMathSharp.Fixed64)1.0,
				(FixedMathSharp.Fixed64)1.0,
				blocked: false);
			p1.ResolvePhase();

			AssertThat(signalReceived).IsTrue();
		}

		// ══════════════════════════════════════════════════════════════
		//  HitboxManager presence and wiring
		// ══════════════════════════════════════════════════════════════

		/// <summary>Both players have HitboxManager nodes wired by GameCoordinator.</summary>
		[TestCase]
		public async Task HitboxManagers_Wired()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			var p2 = runner.FindChild("Player2") as PlayerBridge;

			// ActiveHitboxManager is set by GameCoordinator._Ready()
			AssertThat(p1!.ActiveHitboxManager).IsNotNull();
			AssertThat(p2!.ActiveHitboxManager).IsNotNull();
		}

		/// <summary>Opponents are cross-wired by GameCoordinator.</summary>
		[TestCase]
		public async Task Opponents_Cross_Wired()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			var p2 = runner.FindChild("Player2") as PlayerBridge;

			AssertThat(p1!.Opponent).IsSame(p2);
			AssertThat(p2!.Opponent).IsSame(p1);
		}

		// ══════════════════════════════════════════════════════════════
		//  Signal emission
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public async Task ParrySuccess_Signal_Emitted()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);
			var p1 = runner.FindChild("Player1") as PlayerBridge;
			bool signalReceived = false;
			p1!.ParrySuccess += () => signalReceived = true;
			p1.NotifyParrySuccess();
			p1.ResolvePhase(); // emits events
			AssertThat(signalReceived).IsTrue();
		}

		[TestCase]
		public async Task ShatterWhiff_Signal_Emitted()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);
			var p1 = runner.FindChild("Player1") as PlayerBridge;
			bool signalReceived = false;
			p1!.ShatterWhiff += () => signalReceived = true;
			p1.NotifyShatterWhiff();
			p1.ResolvePhase();
			AssertThat(signalReceived).IsTrue();
		}

		[TestCase]
		public async Task ClashEvent_Signal_Emitted()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);
			var p1 = runner.FindChild("Player1") as PlayerBridge;
			bool signalReceived = false;
			p1!.Clash += () => signalReceived = true;
			p1.NotifyClash();
			p1.ResolvePhase();
			AssertThat(signalReceived).IsTrue();
		}

		// ══════════════════════════════════════════════════════════════
		//  FullReset
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public async Task FullReset_Restores_Economy()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);
			var p1 = runner.FindChild("Player1") as PlayerBridge;
			// Damage vitality and composure
			p1!.ReceiveHit((FixedMathSharp.Fixed64)1.0, (FixedMathSharp.Fixed64)1.0,
				blocked: false, Simulation.AttackTier.Standard, staggerFrames: 12);
			AssertThat((double)p1.GetVitality()).IsLess(1.0);
			// Reset
			p1.FullReset(p1.GlobalPosition);
			AssertThat((double)p1.GetVitality()).IsEqualApprox(1.0, 0.001);
			AssertThat((double)p1.GetComposure()).IsEqualApprox(0.0, 0.001);
			AssertThat((double)p1.GetMomentum()).IsEqualApprox(4.0, 0.001);
		}

		// ══════════════════════════════════════════════════════════════
		//  Premature Press Penalty (§7.6) — Bridge accessors
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public async Task Bridge_Penalty_Accessors_Initial_Values()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);
			var p1 = runner.FindChild("Player1") as PlayerBridge;
			AssertThat(p1!.GetPrematureBlockPenalties()).IsEqual(0);
			AssertThat(p1.GetEffectiveParryWindow()).IsEqual(6);
		}

		// ══════════════════════════════════════════════════════════════
		//  RoundManager signal flow (§11)
		// ══════════════════════════════════════════════════════════════

		/// <summary>
		/// When P1 emits DeathblowTriggered, RoundManager should decrement P2's lives
		/// and emit LivesChanged. This tests the full signal chain:
		/// PlayerBridge.DeathblowTriggered → RoundManager.OnPlayer1Deathblow → _p2Lives--
		/// </summary>
		[TestCase]
		public async Task RoundManager_P1_Deathblow_Signal_Decrements_P2_Lives()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(3); // allow _Ready + deferred StartRound

			var rm = runner.FindChild("RoundManager") as Bridge.RoundManager;
			var p1 = runner.FindChild("Player1") as PlayerBridge;
			int startLives = rm!.P2Lives;

			// Trigger the Deathblow signal from P1 (P1 lands deathblow, P2 loses life)
			p1!.NotifyDeathblowTriggered();
			p1.ResolvePhase(); // EmitCombatEvents → emits DeathblowTriggered signal
			await runner.SimulateFrames(1);

			AssertThat(rm.P2Lives).IsEqual(startLives - 1);
		}

		/// <summary>
		/// RoundManager LivesChanged signal is emitted after a deathblow.
		/// </summary>
		[TestCase]
		public async Task RoundManager_LivesChanged_Emitted_After_Deathblow()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(3);

			var rm = runner.FindChild("RoundManager") as Bridge.RoundManager;
			var p1 = runner.FindChild("Player1") as PlayerBridge;

			bool livesChangedFired = false;
			rm!.LivesChanged += (_, _) => livesChangedFired = true;

			p1!.NotifyDeathblowTriggered();
			p1.ResolvePhase();
			await runner.SimulateFrames(1);

			AssertThat(livesChangedFired).IsTrue();
		}

		// ══════════════════════════════════════════════════════════════
		//  FatigueEntered / FatigueExited edge detection
		// ══════════════════════════════════════════════════════════════

		/// <summary>
		/// §4.1 Fatigue: FatigueEntered fires the first frame Momentum reaches 0.
		/// Drain sequence: TryInitiateShatter(−3.0) → NotifyParrySuccess × 2 (−0.5 each)
		/// takes Momentum from 4.0 → 1.0 → 0.5 → 0.0.
		/// </summary>
		[TestCase]
		public async Task FatigueEntered_Signal_Fired_On_Momentum_Depletion()
		{
			using var runner = ISceneRunner.Load(GameScene);
			await runner.SimulateFrames(2);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			bool fatigueEntered = false;
			p1!.FatigueEntered += () => fatigueEntered = true;

			// 4.0 → 1.0: spend 3.0 via shatter
			p1.TryInitiateShatter();
			p1.ResolvePhase();
			await runner.SimulateFrames(1);
			AssertThat(fatigueEntered).IsFalse(); // 1.0 momentum — not fatigued yet

			// 1.0 → 0.5: parry costs 0.5
			p1.NotifyParrySuccess();
			p1.ResolvePhase();
			await runner.SimulateFrames(1);
			AssertThat(fatigueEntered).IsFalse(); // 0.5 momentum — still not fatigued

			// 0.5 → 0.0: parry costs 0.5 → IsFatigued = true
			p1.NotifyParrySuccess();
			p1.ResolvePhase(); // EmitCombatEvents detects edge: !_wasFatigued → emit FatigueEntered
			await runner.SimulateFrames(1);
			AssertThat(p1.GetIsFatigued()).IsTrue();
			AssertThat(fatigueEntered).IsTrue();
		}
	}
}
