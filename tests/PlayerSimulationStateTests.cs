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
			// 5 more ticks in parrying (total 6 frames including initial)
			for (int i = 0; i < 5; i++)
			{
				AssertThat(sim.IsParrying).IsTrue();
				sim.Tick(EmptyInput());
			}
			// After 6 frames, should transition to Blocking
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
			// Start T0 attack (shortest: 4 coil + 2 swing + 4 recovery = 10 frames)
			sim.Tick(AttackInput(AttackTier.Light));
			// Wait until near end of recovery, then buffer an attack
			// Total: coil=4, swing=2, recovery=4. Frame 1 was the attack input.
			TickN(sim, 7); // frame 8 of 10
			sim.Tick(AttackInput(AttackTier.Light)); // buffer during recovery
													 // Finish recovery
			sim.Tick(EmptyInput()); // frame 10 → idle
									// The buffered attack should now be consumed → Coil
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
	}
}
