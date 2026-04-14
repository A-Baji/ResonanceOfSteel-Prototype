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

		// ── RoW momentum generation (displacement-based) ──────────────

		[TestCase]
		public void RoW_Walk_Toward_Generates_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // start at 0
													 // First tick initializes position tracking. Second tick produces displacement.
			sim.Tick(MoveForwardInputAt(Fixed64.Zero));
			sim.Tick(MoveForwardInputAt((Fixed64)0.067)); // ~1 frame of walk (4.0/60)
			AssertThat((double)sim.Economy.Momentum).IsGreater(0.0);
		}

		[TestCase]
		public void RoW_Larger_Displacement_Generates_More()
		{
			// Run displacement (7/60 ≈ 0.117) > walk displacement (4/60 ≈ 0.067)
			// → more momentum gain per frame at higher speed
			var sim1 = CreateSim();
			sim1.Economy.SpendMomentum((Fixed64)4.0);
			sim1.Tick(MoveForwardInputAt(Fixed64.Zero));
			sim1.Tick(MoveForwardInputAt((Fixed64)0.067)); // walk-pace
			double walkMomentum = (double)sim1.Economy.Momentum;

			var sim2 = CreateSim();
			sim2.Economy.SpendMomentum((Fixed64)4.0);
			sim2.Tick(MoveForwardInputAt(Fixed64.Zero));
			sim2.Tick(MoveForwardInputAt((Fixed64)0.117)); // run-pace
			double runMomentum = (double)sim2.Economy.Momentum;

			AssertThat(runMomentum).IsGreater(walkMomentum);
		}

		[TestCase]
		public void RoW_No_Displacement_No_Momentum()
		{
			// Pressing toward opponent but not actually moving (e.g. blocked by wall/opponent)
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.Tick(MoveForwardInputAt(Fixed64.Zero));
			sim.Tick(MoveForwardInputAt(Fixed64.Zero)); // same position = no displacement
			AssertThat((double)sim.Economy.Momentum).IsEqual(0.0);
		}

		[TestCase]
		public void RoW_Strafe_Does_Not_Generate_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.Tick(MoveSidewaysInput());
			AssertThat((double)sim.Economy.Momentum).IsEqual(0.0);
		}

		[TestCase]
		public void RoW_Retreat_Drains_Momentum()
		{
			var sim = CreateSim();
			// Start at 4.0 momentum
			double before = (double)sim.Economy.Momentum;
			// Initialize position, then retreat
			sim.Tick(MoveBackwardInputAt((Fixed64)1.0));
			sim.Tick(MoveBackwardInputAt((Fixed64)0.933)); // moved 0.067 away from opponent
			double after = (double)sim.Economy.Momentum;
			AssertThat(after).IsLess(before);
		}

		[TestCase]
		public void RoW_Out_Of_Range_No_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			// Opponent at Z=5. Position at Z=-10.2 → distance = 15.2 > RoWMaxRange (15).
			// Move toward opponent (Z=-10.2 → Z=-10.1) but beyond range → no momentum.
			var farInput1 = new PlayerInput(
				moveX: Fixed64.Zero, moveZ: Fixed64.One, runHeld: false,
				attackJustPressed: false, blockParryHeld: false, blockParryJustPressed: false,
				dodgeJustPressed: false, jumpJustPressed: false, modifierTier: AttackTier.Standard,
				isGrounded: true, ownPosX: DefaultOwnX, ownPosZ: (Fixed64)(-10.2),
				opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ);
			var farInput2 = new PlayerInput(
				moveX: Fixed64.Zero, moveZ: Fixed64.One, runHeld: false,
				attackJustPressed: false, blockParryHeld: false, blockParryJustPressed: false,
				dodgeJustPressed: false, jumpJustPressed: false, modifierTier: AttackTier.Standard,
				isGrounded: true, ownPosX: DefaultOwnX, ownPosZ: (Fixed64)(-10.1),
				opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ);
			sim.Tick(farInput1);
			sim.Tick(farInput2);
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
			// Initialize position, then block-walk with displacement
			sim.Tick(BlockWalkForwardInputAt(Fixed64.Zero));
			sim.Tick(BlockWalkForwardInputAt((Fixed64)0.033)); // ~1 frame of block-walk (2.0/60)
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

		// ══════════════════════════════════════════════════════════════
		//  Moving state: transitions and input handling
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Moving_To_Blocking_On_BlockHeld()
		{
			// ProcessMoving → ProcessActionableInput handles BlockParryHeld via default branch
			var sim = CreateSim();
			sim.Tick(MoveForwardInput()); // enter Moving
			AssertThat(sim.DebugStateName).IsEqual("Moving");
			sim.Tick(BlockWalkForwardInput()); // hold block while moving → Blocking
			AssertThat(sim.IsBlocking).IsTrue();
		}

		[TestCase]
		public void Attack_Buffered_During_Blocking_Fires_After_Release()
		{
			// Attack pressed while blocking goes to buffer; consumed when block is released
			var sim = CreateSim();
			sim.Tick(BlockHeldInput()); // enter Blocking
										// Press attack while holding block (attack buffered, block continues)
			var blockWithAttack = new PlayerInput(
				moveX: Fixed64.Zero, moveZ: Fixed64.Zero,
				runHeld: false, attackJustPressed: true,
				blockParryHeld: true, blockParryJustPressed: false,
				dodgeJustPressed: false, jumpJustPressed: false,
				modifierTier: AttackTier.Standard, isGrounded: true,
				ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
				opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ);
			sim.Tick(blockWithAttack);
			AssertThat(sim.IsBlocking).IsTrue();
			AssertThat(sim.DebugBufferCount).IsEqual(1);
			sim.Tick(EmptyInput()); // release block → transitions to Idle
			sim.Tick(EmptyInput()); // Idle: buffer consumed → Coil
			AssertThat(sim.DebugStateName).Contains("Coil");
		}

		[TestCase]
		public void BlockParry_Buffered_During_Recovery_Executes_As_Parry()
		{
			// block_parry pressed late in Recovery stays buffered and fires as Parrying
			// when the sim returns to Idle (within TTL window)
			var sim = CreateSim();
			AdvanceToRecovery(sim, AttackTier.Light); // Recovery(4, T0)
			TickN(sim, 3); // countdown to Recovery(1)
			sim.Tick(BlockParryPressInput()); // buffered; Recovery(1) → Idle this tick
			sim.Tick(EmptyInput()); // Idle: buffer consumed → Parrying
			AssertThat(sim.IsParrying).IsTrue();
		}

		// ══════════════════════════════════════════════════════════════
		//  PREMATURE PRESS PENALTY (§7.6)
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Initial_Penalty_Is_Zero()
		{
			var sim = CreateSim();
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(6);
		}

		[TestCase]
		public void Parry_Whiff_Increments_Penalty()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput()); // enter Parrying(6)
			TickN(sim, 6); // exhaust window → Blocking, no OnParrySuccess
			AssertThat(sim.PrematureBlockPenalties).IsEqual(1);
		}

		[TestCase]
		public void One_Penalty_Halves_Window_To_3()
		{
			var sim = CreateSim();
			// Whiff once
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(3);
		}

		[TestCase]
		public void Two_Penalties_Reduce_Window_To_2()
		{
			var sim = CreateSim();
			// Whiff twice
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			sim.Tick(EmptyInput()); // exit blocking
			sim.Tick(BlockParryPressInput());
			TickN(sim, 3); // effective window is now 3
			AssertThat(sim.PrematureBlockPenalties).IsEqual(2);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(2);
		}

		[TestCase]
		public void Three_Penalties_Reduce_Window_To_1()
		{
			var sim = CreateSim();
			// Whiff three times
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			sim.Tick(EmptyInput());
			sim.Tick(BlockParryPressInput());
			TickN(sim, 3);
			sim.Tick(EmptyInput());
			sim.Tick(BlockParryPressInput());
			TickN(sim, 2);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(3);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(1);
		}

		[TestCase]
		public void Penalty_Clamped_At_One_Frame_Minimum()
		{
			var sim = CreateSim();
			// Whiff four times
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			sim.Tick(EmptyInput());
			sim.Tick(BlockParryPressInput());
			TickN(sim, 3);
			sim.Tick(EmptyInput());
			sim.Tick(BlockParryPressInput());
			TickN(sim, 2);
			sim.Tick(EmptyInput());
			sim.Tick(BlockParryPressInput());
			TickN(sim, 1);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(4);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(1);
		}

		[TestCase]
		public void Parry_Success_Resets_Penalty()
		{
			var sim = CreateSim();
			// Whiff once
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(1);
			// Successful parry
			sim.OnParrySuccess();
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(6);
		}

		[TestCase]
		public void Parry_Not_Penalized_When_Successful()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput()); // enter Parrying(6)
			sim.OnParrySuccess(); // successful parry during window
			TickN(sim, 5); // exhaust remaining window → Blocking
						   // Penalty should NOT have incremented because success flag was set
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
		}

		[TestCase]
		public void Penalty_Reset_On_ResetState()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6); // whiff
			AssertThat(sim.PrematureBlockPenalties).IsEqual(1);
			sim.ResetState();
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(6);
		}

		[TestCase]
		public void Penalized_Parry_Window_Actually_Shorter()
		{
			var sim = CreateSim();
			// Whiff once → window = 3
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			sim.Tick(EmptyInput()); // exit blocking
									// Enter parry again — should have 3-frame window
			sim.Tick(BlockParryPressInput());
			AssertThat(sim.IsParrying).IsTrue();
			// After 3 ticks, should be in Blocking (not still Parrying)
			TickN(sim, 3);
			AssertThat(sim.IsBlocking).IsTrue();
		}

		[TestCase]
		public void Penalty_Decays_After_Inactivity()
		{
			var sim = CreateSim();
			// Whiff once → penalty = 1
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(1);
			sim.Tick(EmptyInput()); // exit blocking
									// Tick 30 empty frames (inactivity threshold)
			TickN(sim, 30);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(6);
		}

		[TestCase]
		public void Penalty_Not_Decayed_Before_Inactivity_Threshold()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6); // whiff → penalty = 1
			sim.Tick(EmptyInput()); // exit blocking
									// Tick only 22 frames — not enough for reset
									// (6 parry ticks + 1 exit + 22 = 29 frames since press, under threshold)
			TickN(sim, 22);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(1);
		}

		[TestCase]
		public void Penalty_Inactivity_Resets_Effective_Window()
		{
			var sim = CreateSim();
			// Whiff twice → penalty = 2, window = 2
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			sim.Tick(EmptyInput());
			sim.Tick(BlockParryPressInput());
			TickN(sim, 3); // effective window = 3, whiff after 3
			AssertThat(sim.PrematureBlockPenalties).IsEqual(2);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(2);
			sim.Tick(EmptyInput());
			// Wait for inactivity reset (30 frames from last press)
			TickN(sim, 30);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(6);
		}

		[TestCase]
		public void Penalty_Inactivity_Timer_Resets_On_New_Press()
		{
			var sim = CreateSim();
			// Whiff once → penalty = 1
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			sim.Tick(EmptyInput());
			// Wait 20 frames (not enough for reset)
			TickN(sim, 20);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(1);
			// Press block again — resets the inactivity timer
			sim.Tick(BlockParryPressInput());
			TickN(sim, 3); // whiff (window was 3) → penalty = 2
			AssertThat(sim.PrematureBlockPenalties).IsEqual(2);
			sim.Tick(EmptyInput());
			// Now wait 29 frames — still not enough from the NEW press
			TickN(sim, 25);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(2);
		}

		[TestCase]
		public void RoW_Not_Generated_During_ActionLock()
		{
			var sim = CreateSim();
			double before = (double)sim.Economy.Momentum;
			sim.Tick(AttackInput()); // enter Coil (action-locked)
									 // Tick with forward movement during action lock
			sim.Tick(MoveForwardInput());
			double after = (double)sim.Economy.Momentum;
			// No RoW momentum should be generated during action lock
			AssertThat(after).IsEqual(before);
		}

		[TestCase]
		public void RoW_Moving_Away_No_Momentum()
		{
			var sim = CreateSim();
			double before = (double)sim.Economy.Momentum;
			// Move backward (away from opponent at +5Z)
			var backwardInput = new PlayerInput(
				moveX: Fixed64.Zero, moveZ: -(Fixed64)1.0,
				runHeld: false,
				attackJustPressed: false,
				blockParryHeld: false, blockParryJustPressed: false,
				dodgeJustPressed: false, jumpJustPressed: false,
				modifierTier: AttackTier.Standard,
				isGrounded: true,
				ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
				opponentPosX: DefaultOppX, opponentPosZ: DefaultOppZ
			);
			sim.Tick(backwardInput);
			double after = (double)sim.Economy.Momentum;
			AssertThat(after).IsEqual(before);
		}

		// ══════════════════════════════════════════════════════════════
		//  IsMovingToward edge cases
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void IsMovingToward_Returns_False_When_At_Same_Position()
		{
			// §5: distSq == 0 guard — opponent at exact same XZ position.
			// With distSq == 0 the dot-product check short-circuits to false.
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // start at 0
			var samePos = new PlayerInput(
				moveX: Fixed64.Zero, moveZ: Fixed64.One,
				runHeld: false,
				attackJustPressed: false,
				blockParryHeld: false, blockParryJustPressed: false,
				dodgeJustPressed: false, jumpJustPressed: false,
				modifierTier: AttackTier.Standard,
				isGrounded: true,
				ownPosX: DefaultOwnX, ownPosZ: DefaultOwnZ,
				opponentPosX: DefaultOwnX, opponentPosZ: DefaultOwnZ // same as own
			);
			sim.Tick(samePos);
			// No RoW generated when opponent is at the same position
			AssertThat((double)sim.Economy.Momentum).IsEqual(0.0);
		}
	}
}
