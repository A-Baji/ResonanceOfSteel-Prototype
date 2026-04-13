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

			// Block + run + move
			runner.SimulateActionPress("block_parry");
			runner.SimulateActionPress("run");
			runner.SimulateActionPress("move_up");
			await runner.SimulateFrames(9);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			// Should NOT be running — blocking suppresses run
			AssertThat(p1!.GetDebugStateName()).IsEqual("Blocking");
			// Velocity should be at BlockWalkSpeed (2.0), not RunSpeed (7.0)
			float speed = new Vector2(p1.Velocity.X, p1.Velocity.Z).Length();
			AssertThat((double)speed).IsLessEqual(2.5); // small tolerance for physics

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

			// Attack while moving — should slide to stop
			runner.SimulateActionRelease("move_up");
			runner.SimulateActionPress("attack");
			await runner.SimulateFrames(1);

			var p1 = runner.FindChild("Player1") as PlayerBridge;
			// Should be in Coil AND still have some residual velocity (slide)
			AssertThat(p1!.GetDebugStateName()).Contains("Coil");
			// Velocity should not be exactly zero on the first frame of commitment
			// (it decays via ActionLockFriction)
			float speed = new Vector2(p1.Velocity.X, p1.Velocity.Z).Length();
			AssertThat((double)speed).IsGreater(0.0);
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

			// Trigger attack → advance to swing → notify hit landed directly
			runner.SimulateActionPressed("attack");
			await runner.SimulateFrames(10); // get past coil into swing

			// Use bridge API to directly notify a hit (bypassing physics)
			p1!.NotifyHitLanded(
				(FixedMathSharp.Fixed64)1.0,
				(FixedMathSharp.Fixed64)1.0,
				blocked: false);

			// The signal should emit on the next frame when EmitCombatEvents runs
			await runner.SimulateFrames(1);
			AssertThat(await AssertSignal(p1).IsEmitted("HitLanded")).IsTrue();
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
	}
}
