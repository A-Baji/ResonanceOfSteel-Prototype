using GdUnit4;
using static GdUnit4.Assertions;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;
using static ResonanceOfSteel.Tests.TestHelpers;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	public partial class EvasionSystemTests
	{
		// ══════════════════════════════════════════════════════════════
		//  DODGE
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Dodge_Enters_Startup()
		{
			var sim = CreateSim();
			sim.Tick(DodgeInput());
			AssertThat(sim.DebugStateName).Contains("Dodge Startup");
		}

		[TestCase]
		public void Dodge_Startup_To_Active()
		{
			var sim = CreateSim();
			sim.Tick(DodgeInput());
			TickN(sim, 3); // 3 startup frames
			AssertThat(sim.DebugStateName).Contains("Dodge Active");
		}

		[TestCase]
		public void Dodge_Active_To_Recovery()
		{
			var sim = CreateSim();
			sim.Tick(DodgeInput());
			TickN(sim, 3 + 12); // 3 startup + 12 active
			AssertThat(sim.DebugStateName).Contains("Dodge Recovery");
		}

		[TestCase]
		public void Dodge_Full_Cycle_Returns_To_Idle()
		{
			var sim = CreateSim();
			sim.Tick(DodgeInput());
			TickN(sim, 3 + 12 + 3 + 1); // 19 ticks: 3 startup + 12 active + 3 recovery + 1 exit
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		[TestCase]
		public void Dodge_Is_Action_Locked()
		{
			var sim = CreateSim();
			sim.Tick(DodgeInput());
			AssertThat(sim.IsActionLocked).IsTrue();
		}

		[TestCase]
		public void Dodge_Costs_Momentum()
		{
			var sim = CreateSim();
			double before = (double)sim.Economy.Momentum;
			sim.Tick(DodgeInput());
			double after = (double)sim.Economy.Momentum;
			AssertThat(before - after).IsEqualApprox(1.5, 0.001);
		}

		[TestCase]
		public void Dodge_Forward_Rejected()
		{
			var sim = CreateSim();
			sim.Tick(DodgeForwardInput());
			// Dodge toward opponent should not enter Dodging
			AssertThat(sim.DebugStateName).IsNotEqual("Dodge Startup [2f]");
			AssertThat(sim.DebugStateName).IsNotEqual("Dodge Startup [3f]");
		}

		[TestCase]
		public void Dodge_No_Movement_Rejected()
		{
			// Dodge press without stick input should not trigger dodge
			var input = new PlayerInput(
				moveX: Fixed64.Zero, moveZ: Fixed64.Zero,
				runHeld: false,
				attackJustPressed: false,
				blockParryHeld: false, blockParryJustPressed: false,
				dodgeJustPressed: true, jumpJustPressed: false,
				modifierTier: AttackTier.Standard,
				isGrounded: true,
				ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
				opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
			);
			var sim = CreateSim();
			sim.Tick(input);
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ── Dodge fatigue degradation ──────────────────────────────────

		[TestCase]
		public void Dodge_Fatigued_Extra_Startup()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // fatigue
			sim.Tick(DodgeInput());
			// Fatigued: 3+4=7 startup, 12-4=8 active, 3 recovery
			AssertThat(sim.DebugStateName).IsEqual("Dodge Startup [7f]");
		}

		[TestCase]
		public void Dodge_Fatigued_Reduced_Active()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.Tick(DodgeInput());
			TickN(sim, 7); // 7 startup frames for fatigued
			AssertThat(sim.DebugStateName).IsEqual("Dodge Active [8f]");
		}

		[TestCase]
		public void Dodge_Available_During_Fatigue()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // zero momentum
			AssertThat(sim.Economy.IsFatigued).IsTrue();
			sim.Tick(DodgeInput());
			AssertThat(sim.DebugStateName).Contains("Dodge");
		}

		// ══════════════════════════════════════════════════════════════
		//  JUMP
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Jump_Enters_Startup()
		{
			var sim = CreateSim();
			sim.Tick(JumpInput());
			AssertThat(sim.DebugStateName).Contains("Jump Startup");
		}

		[TestCase]
		public void Jump_Startup_To_Active()
		{
			var sim = CreateSim();
			sim.Tick(JumpInput());
			TickN(sim, 3); // 3 startup frames
			AssertThat(sim.DebugStateName).Contains("Jump Active");
		}

		[TestCase]
		public void Jump_Active_To_Recovery()
		{
			var sim = CreateSim();
			sim.Tick(JumpInput());
			TickN(sim, 3); // startup done
			var airborne = AirborneInput();
			for (int i = 0; i < 22; i++) sim.Tick(airborne); // active done
			AssertThat(sim.DebugStateName).Contains("Jump Recovery");
		}

		[TestCase]
		public void Jump_Full_Cycle_Returns_To_Idle()
		{
			var sim = CreateSim();
			sim.Tick(JumpInput());
			TickN(sim, 3 + 22 + 5); // 30 total
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		[TestCase]
		public void Jump_Is_Action_Locked()
		{
			var sim = CreateSim();
			sim.Tick(JumpInput());
			AssertThat(sim.IsActionLocked).IsTrue();
		}

		[TestCase]
		public void Jump_Costs_Momentum()
		{
			var sim = CreateSim();
			double before = (double)sim.Economy.Momentum;
			sim.Tick(JumpInput());
			double after = (double)sim.Economy.Momentum;
			AssertThat(before - after).IsEqualApprox(1.0, 0.001);
		}

		[TestCase]
		public void Jump_Early_Landing_On_Ground_Contact()
		{
			var sim = CreateSim();
			sim.Tick(JumpInput());
			TickN(sim, 3); // complete startup, enter active

			// Send grounded input during active phase
			var grounded = GroundedInput();
			sim.Tick(grounded);
			AssertThat(sim.DebugStateName).Contains("Jump Recovery");
		}

		[TestCase]
		public void Jump_Airborne_Continues_Active()
		{
			var sim = CreateSim();
			sim.Tick(JumpInput());
			TickN(sim, 3); // complete startup, in active
			sim.Tick(AirborneInput()); // not grounded
			AssertThat(sim.DebugStateName).Contains("Jump Active");
		}

		// ── Jump fatigue degradation ───────────────────────────────────

		[TestCase]
		public void Jump_Fatigued_Extra_Startup()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // fatigue
			sim.Tick(JumpInput());
			// Fatigued: 3+4=7 startup, 22-4=18 active, 5 recovery
			AssertThat(sim.DebugStateName).IsEqual("Jump Startup [7f]");
		}

		[TestCase]
		public void Jump_Fatigued_Reduced_Active()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.Tick(JumpInput());
			TickN(sim, 7); // 7 startup frames for fatigued
			AssertThat(sim.DebugStateName).IsEqual("Jump Active [18f]");
		}

		[TestCase]
		public void Jump_Available_During_Fatigue()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			AssertThat(sim.Economy.IsFatigued).IsTrue();
			sim.Tick(JumpInput());
			AssertThat(sim.DebugStateName).Contains("Jump");
		}

		// ── No direction required for jump ─────────────────────────────

		[TestCase]
		public void Jump_No_Direction_Still_Works()
		{
			var sim = CreateSim();
			sim.Tick(JumpInput()); // JumpInput has no movement
			AssertThat(sim.DebugStateName).Contains("Jump");
		}

		// ── Evasion during action lock ─────────────────────────────────

		[TestCase]
		public void Dodge_During_Attack_Rejected()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput()); // enter Coil
			sim.Tick(DodgeInput());  // try to dodge while in Coil
			AssertThat(sim.DebugStateName).Contains("Coil");
		}

		[TestCase]
		public void Jump_During_Attack_Rejected()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput());
			sim.Tick(JumpInput());
			AssertThat(sim.DebugStateName).Contains("Coil");
		}

		// ── Dodge backward (valid direction) ───────────────────────────

		[TestCase]
		public void Dodge_Backward_Accepted()
		{
			// Moving away from opponent (negative Z when opponent is at +Z)
			var input = new PlayerInput(
				moveX: Fixed64.Zero, moveZ: -(Fixed64)1.0,
				runHeld: false,
				attackJustPressed: false,
				blockParryHeld: false, blockParryJustPressed: false,
				dodgeJustPressed: true, jumpJustPressed: false,
				modifierTier: AttackTier.Standard,
				isGrounded: true,
				ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
				opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
			);
			var sim = CreateSim();
			sim.Tick(input);
			AssertThat(sim.DebugStateName).Contains("Dodge");
		}

		// ── Total frame counts from spec ───────────────────────────────

		[TestCase]
		public void Dodge_Total_18_Frames()
		{
			// §7.3: 3 startup + 12 active + 3 recovery = 18 total
			var sim = CreateSim();
			sim.Tick(DodgeInput()); // enters Dodging
			// Tick 18 more frames to complete the dodge + 1 to exit
			TickN(sim, 18 + 1);
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		[TestCase]
		public void Jump_Total_30_Frames()
		{
			// §7.3: 3 startup + 22 active + 5 recovery = 30 total
			var sim = CreateSim();
			sim.Tick(JumpInput());
			TickN(sim, 30);
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ── Dodge fatigued total still 18 frames ───────────────────────

		[TestCase]
		public void Dodge_Fatigued_Total_Still_18_Frames()
		{
			// §7.3: Fatigued = +4 startup, -4 active: 7+8+3 = 18
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.Tick(DodgeInput());
			TickN(sim, 18 + 1);
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ── Jump fatigued total still 30 frames ────────────────────────

		[TestCase]
		public void Jump_Fatigued_Total_Still_30_Frames()
		{
			// §7.3: Fatigued = +4 startup, -4 active: 7+18+5 = 30
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.Tick(JumpInput());
			TickN(sim, 30);
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ── Momentum NOT deducted when can't afford evasion ────────────

		[TestCase]
		public void Dodge_Unaffordable_No_Momentum_Deducted()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // drain to 0
			double before = (double)sim.Economy.Momentum;
			sim.Tick(DodgeInput());
			// Can't afford → no deduction (already at 0)
			AssertThat((double)sim.Economy.Momentum).IsEqual(before);
		}

		[TestCase]
		public void Jump_Unaffordable_No_Momentum_Deducted()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			double before = (double)sim.Economy.Momentum;
			sim.Tick(JumpInput());
			AssertThat((double)sim.Economy.Momentum).IsEqual(before);
		}

		// ── Consecutive evasion after first completes ──────────────────

		[TestCase]
		public void Consecutive_Dodge_Is_Possible_After_First_Completes()
		{
			// §7.3: After 18-frame dodge, player returns to Idle — can immediately dodge again
			var sim = CreateSim();
			sim.Tick(DodgeInput()); // first dodge
			TickN(sim, 19); // complete full cycle (18 + 1 to exit Recovery)
			AssertThat(sim.DebugStateName).IsEqual("Idle");
			sim.Tick(DodgeInput()); // second dodge
			AssertThat(sim.DebugStateName).Contains("Dodge");
		}

		[TestCase]
		public void Consecutive_Jump_Is_Possible_After_First_Completes()
		{
			// After 30-frame jump, player returns to Idle — can immediately jump again
			var sim = CreateSim();
			sim.Tick(JumpInput());
			TickN(sim, 30); // complete full cycle
			AssertThat(sim.DebugStateName).IsEqual("Idle");
			sim.Tick(JumpInput()); // second jump
			AssertThat(sim.DebugStateName).Contains("Jump");
		}
	}
}
