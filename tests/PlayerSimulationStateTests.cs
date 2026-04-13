using GdUnit4;
using static GdUnit4.Assertions;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;
using static ResonanceOfSteel.Tests.TestHelpers;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	public partial class PlayerSimulationStateTests
	{
		// ── Initial state ──────────────────────────────────────────────

		[TestCase]
		public void Initial_State_Is_Idle()
		{
			var sim = CreateSim();
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		[TestCase]
		public void Initial_State_Not_ActionLocked()
		{
			var sim = CreateSim();
			AssertThat(sim.IsActionLocked).IsFalse();
		}

		[TestCase]
		public void Initial_HitboxActive_False()
		{
			var sim = CreateSim();
			sim.Tick(EmptyInput());
			AssertThat(sim.HitboxActive).IsFalse();
		}

		// ── Movement states ────────────────────────────────────────────

		[TestCase]
		public void Movement_Input_Transitions_To_Moving()
		{
			var sim = CreateSim();
			sim.Tick(MoveForwardInput());
			AssertThat(sim.DebugStateName).IsEqual("Moving");
		}

		[TestCase]
		public void Run_Input_Transitions_To_Running()
		{
			var sim = CreateSim();
			sim.Tick(MoveForwardInput(running: true));
			AssertThat(sim.DebugStateName).IsEqual("Running");
		}

		[TestCase]
		public void Moving_Not_ActionLocked()
		{
			var sim = CreateSim();
			sim.Tick(MoveForwardInput());
			AssertThat(sim.IsActionLocked).IsFalse();
		}

		[TestCase]
		public void Release_Stick_Returns_To_Idle()
		{
			var sim = CreateSim();
			sim.Tick(MoveForwardInput());
			sim.Tick(EmptyInput());
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ── Blocking ───────────────────────────────────────────────────

		[TestCase]
		public void Block_Held_Enters_Blocking()
		{
			var sim = CreateSim();
			sim.Tick(BlockHeldInput());
			AssertThat(sim.IsBlocking).IsTrue();
		}

		[TestCase]
		public void Blocking_Not_ActionLocked()
		{
			var sim = CreateSim();
			sim.Tick(BlockHeldInput());
			AssertThat(sim.IsActionLocked).IsFalse();
		}

		[TestCase]
		public void Release_Block_Returns_To_Idle()
		{
			var sim = CreateSim();
			sim.Tick(BlockHeldInput());
			sim.Tick(EmptyInput());
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		[TestCase]
		public void Block_No_Momentum_Cost()
		{
			var sim = CreateSim();
			double momentumBefore = (double)sim.Economy.Momentum;
			sim.Tick(BlockHeldInput());
			AssertThat((double)sim.Economy.Momentum).IsEqual(momentumBefore);
		}

		[TestCase]
		public void Block_Available_During_Fatigue()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // fatigue
			sim.Tick(BlockHeldInput());
			AssertThat(sim.IsBlocking).IsTrue();
		}

		// ── Parrying ───────────────────────────────────────────────────

		[TestCase]
		public void BlockParry_Press_Enters_Parrying()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput());
			AssertThat(sim.IsParrying).IsTrue();
		}

		[TestCase]
		public void Parrying_Is_ActionLocked()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput());
			AssertThat(sim.IsActionLocked).IsTrue();
		}

		[TestCase]
		public void Parry_Window_Is_Six_Frames()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput());
			// 6 processing ticks to exhaust the parry window
			for (int i = 0; i < 6; i++)
			{
				AssertThat(sim.IsParrying).IsTrue();
				sim.Tick(EmptyInput());
			}
			// After 6 processing ticks, should transition to Blocking
			AssertThat(sim.IsBlocking).IsTrue();
		}

		[TestCase]
		public void Parry_Reverts_To_Block_If_Fatigued()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // fatigue (momentum = 0)
			sim.Tick(BlockParryPressInput());
			// Can't afford parry, should be in Blocking instead
			AssertThat(sim.IsBlocking).IsTrue();
			AssertThat(sim.IsParrying).IsFalse();
		}

		[TestCase]
		public void Parry_Costs_Momentum_On_Success()
		{
			var sim = CreateSim();
			double before = (double)sim.Economy.Momentum;
			sim.Tick(BlockParryPressInput());
			// Parry cost is paid on successful parry notification, not on entering state
			sim.OnParrySuccess();
			AssertThat((double)sim.Economy.Momentum).IsEqual(before - 0.5);
		}

		// ── Action lock during committed states ────────────────────────

		[TestCase]
		public void Coil_Is_ActionLocked()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput());
			AssertThat(sim.IsActionLocked).IsTrue();
		}

		[TestCase]
		public void Swing_Is_ActionLocked()
		{
			var sim = CreateSim();
			AdvanceToSwing(sim, AttackTier.Standard);
			AssertThat(sim.IsActionLocked).IsTrue();
		}

		[TestCase]
		public void Recovery_Is_ActionLocked()
		{
			var sim = CreateSim();
			AdvanceToRecovery(sim, AttackTier.Standard);
			AssertThat(sim.IsActionLocked).IsTrue();
		}

		[TestCase]
		public void Staggered_Is_ActionLocked()
		{
			var sim = CreateSim();
			sim.OnHitReceived(Fixed64.One, Fixed64.One, wasBlocked: false,
				AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.IsActionLocked).IsTrue();
			AssertThat(sim.IsStaggered).IsTrue();
		}

		[TestCase]
		public void Deathblow_Is_ActionLocked()
		{
			var sim = CreateSim();
			sim.OnDeathblowTriggered();
			AssertThat(sim.IsActionLocked).IsTrue();
			AssertThat(sim.IsInDeathblow).IsTrue();
		}

		// ── Buffer preservation during action lock ─────────────────────

		[TestCase]
		public void Buffered_Input_Preserved_During_ActionLock()
		{
			var sim = CreateSim();
			// Start attack (Coil), buffer an attack during it
			sim.Tick(AttackInput());
			// Buffer count should be 0 (consumed)
			AssertThat(sim.DebugBufferCount).IsEqual(0);

			// Press attack again during Coil (action-locked, should buffer)
			sim.Tick(AttackInput());
			AssertThat(sim.DebugBufferCount).IsEqual(1);
		}

		[TestCase]
		public void Buffered_Input_Consumed_After_Return_To_Idle()
		{
			var sim = CreateSim();
			// Start T0 attack (4 coil + 2 swing + 4 recovery = 10 frames)
			sim.Tick(AttackInput(AttackTier.Light));
			TickN(sim, 8); // advance through coil/swing into Recovery(2)
			sim.Tick(AttackInput(AttackTier.Light)); // buffer during Recovery(1)
			sim.Tick(EmptyInput()); // Recovery finishes → Idle (buffer not consumed yet)
			sim.Tick(EmptyInput()); // Idle → buffer consumed → Coil
			AssertThat(sim.DebugStateName).Contains("Coil");
		}

		// ── RoW momentum generation ────────────────────────────────────

		[TestCase]
		public void RoW_Walk_Toward_Generates_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // start at 0
			sim.Tick(MoveForwardInput(running: false));
			AssertThat((double)sim.Economy.Momentum).IsGreater(0.0);
		}

		[TestCase]
		public void RoW_Run_Generates_More_Than_Walk()
		{
			var sim1 = CreateSim();
			sim1.Economy.SpendMomentum((Fixed64)4.0);
			sim1.Tick(MoveForwardInput(running: false));
			double walkMomentum = (double)sim1.Economy.Momentum;

			var sim2 = CreateSim();
			sim2.Economy.SpendMomentum((Fixed64)4.0);
			sim2.Tick(MoveForwardInput(running: true));
			double runMomentum = (double)sim2.Economy.Momentum;

			AssertThat(runMomentum).IsGreater(walkMomentum);
		}

		[TestCase]
		public void RoW_Strafe_Does_Not_Generate_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.Tick(MoveSidewaysInput());
			AssertThat((double)sim.Economy.Momentum).IsEqual(0.0);
		}

		// ── Reset ──────────────────────────────────────────────────────

		[TestCase]
		public void ResetState_Returns_To_Idle()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput());
			sim.ResetState();
			sim.Tick(EmptyInput());
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ── Blocking + movement (block-walk) ───────────────────────────

		[TestCase]
		public void Block_Held_With_Movement_Stays_Blocking()
		{
			var sim = CreateSim();
			sim.Tick(BlockWalkForwardInput());
			AssertThat(sim.IsBlocking).IsTrue();
			// Still blocking, not Moving
			AssertThat(sim.DebugStateName).IsEqual("Blocking");
		}

		[TestCase]
		public void Block_Held_With_Run_Stays_Blocking()
		{
			var sim = CreateSim();
			sim.Tick(BlockWalkForwardInput(running: true));
			// Run is suppressed while blocking — still Blocking state
			AssertThat(sim.IsBlocking).IsTrue();
		}

		[TestCase]
		public void Blocking_Not_Action_Locked_Allows_Movement()
		{
			var sim = CreateSim();
			sim.Tick(BlockWalkForwardInput());
			AssertThat(sim.IsActionLocked).IsFalse();
		}

		// ── Parry window → Blocking transition ─────────────────────────

		[TestCase]
		public void Parry_Exhausted_Transitions_To_Blocking()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput());
			// Exhaust 6-frame parry window
			for (int i = 0; i < 6; i++)
				sim.Tick(EmptyInput());
			AssertThat(sim.IsBlocking).IsTrue();
			AssertThat(sim.IsParrying).IsFalse();
		}

		// ── RoW momentum during Blocking state ─────────────────────────

		[TestCase]
		public void RoW_Momentum_During_Blocking()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.Tick(BlockWalkForwardInput());
			// Walking toward opponent while blocking should generate RoW
			AssertThat((double)sim.Economy.Momentum).IsGreater(0.0);
		}

		// ── Event reset per tick ────────────────────────────────────────

		[TestCase]
		public void LastEvent_Reset_Each_Tick()
		{
			var sim = CreateSim();
			sim.OnParrySuccess();
			AssertThat(sim.LastEvent).IsEqual(CombatEvent.ParrySuccess);
			sim.Tick(EmptyInput());
			AssertThat(sim.LastEvent).IsEqual(CombatEvent.None);
		}

		// ── HitboxActive only during Swing ─────────────────────────────

		[TestCase]
		public void HitboxActive_False_During_Coil()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput());
			sim.Tick(EmptyInput()); // process 1 coil tick
			AssertThat(sim.HitboxActive).IsFalse();
		}

		[TestCase]
		public void HitboxActive_False_During_Recovery()
		{
			var sim = CreateSim();
			AdvanceToRecovery(sim, AttackTier.Standard);
			sim.Tick(EmptyInput()); // process 1 recovery tick
			AssertThat(sim.HitboxActive).IsFalse();
		}
	}
}
