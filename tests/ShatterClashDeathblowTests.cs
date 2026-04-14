using GdUnit4;
using static GdUnit4.Assertions;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;
using static ResonanceOfSteel.Tests.TestHelpers;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	public partial class ShatterClashDeathblowTests
	{
		// ══════════════════════════════════════════════════════════════
		//  SHATTER
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Shatter_Window_Active_After_Block_Press()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput()); // frame 0: press
			AssertThat(sim.IsInShatterWindow).IsTrue();
		}

		[TestCase]
		public void Shatter_Window_Expires_After_ParryFrames()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput());
			// Parry window is 6 frames, advance past it
			TickN(sim, 6);
			AssertThat(sim.IsInShatterWindow).IsFalse();
		}

		[TestCase]
		public void TryInitiateShatter_Succeeds_With_Momentum()
		{
			var sim = CreateSim(); // starts at 4.0 momentum
			bool result = sim.TryInitiateShatter();
			AssertThat(result).IsTrue();
		}

		[TestCase]
		public void TryInitiateShatter_Costs_3_Momentum()
		{
			var sim = CreateSim();
			double before = (double)sim.Economy.Momentum;
			sim.TryInitiateShatter();
			double after = (double)sim.Economy.Momentum;
			AssertThat(before - after).IsEqualApprox(3.0, 0.001);
		}

		[TestCase]
		public void TryInitiateShatter_Fails_Without_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // drain all
			bool result = sim.TryInitiateShatter();
			AssertThat(result).IsFalse();
		}

		[TestCase]
		public void OnShatterLanded_Generates_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.OnShatterLanded();
			AssertThat((double)sim.Economy.Momentum).IsGreater(0.0);
		}

		[TestCase]
		public void OnShatterLanded_Resets_Stacks()
		{
			var sim = CreateSim();
			sim.Economy.IncrementFrameAdvantage();
			sim.Economy.IncrementFrameAdvantage();
			sim.OnShatterLanded();
			AssertThat(sim.Economy.FrameAdvantageStacks).IsEqual(0);
		}

		[TestCase]
		public void OnShatterLanded_Event()
		{
			var sim = CreateSim();
			sim.OnShatterLanded();
			AssertThat(sim.LastEvent).IsEqual(CombatEvent.ShatterEvent);
		}

		[TestCase]
		public void OnShatterWhiff_Event()
		{
			var sim = CreateSim();
			sim.OnShatterWhiff();
			AssertThat(sim.LastEvent).IsEqual(CombatEvent.ShatterWhiff);
		}

		[TestCase]
		public void ShatterWhiff_Adds_Recovery_Frames()
		{
			var sim = CreateSim();
			sim.OnShatterWhiff();
			// Now do an attack — recovery should include penalty
			AdvanceToRecovery(sim, AttackTier.Standard);
			// Standard Longsword T1 recovery = 6, + ShatterWhiffPenaltyFrames(20) = 26
			AssertThat(sim.DebugStateName).IsEqual("Recovery T1 [26f]");
		}

		[TestCase]
		public void ShatterWhiff_Penalty_Consumed_Once()
		{
			var sim = CreateSim();
			sim.OnShatterWhiff();
			CompleteFullAttack(sim, AttackTier.Standard);
			// Second attack should have normal recovery
			AdvanceToRecovery(sim, AttackTier.Standard);
			AssertThat(sim.DebugStateName).IsEqual("Recovery T1 [6f]");
		}

		// ══════════════════════════════════════════════════════════════
		//  CLASH
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void OnClash_Generates_Momentum_Surge()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // drain to 0
			sim.OnClash();
			AssertThat((double)sim.Economy.Momentum).IsEqualApprox(2.0, 0.001);
		}

		[TestCase]
		public void OnClash_Enters_Recovery()
		{
			var sim = CreateSim();
			sim.OnClash();
			AssertThat(sim.DebugStateName).Contains("Recovery");
		}

		[TestCase]
		public void OnClash_Recovery_Duration()
		{
			var sim = CreateSim();
			sim.OnClash();
			// ClashRecoveryFrames = 8
			TickN(sim, 8);
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		[TestCase]
		public void OnClash_Sets_Event()
		{
			var sim = CreateSim();
			sim.OnClash();
			AssertThat(sim.LastEvent).IsEqual(CombatEvent.ClashEvent);
		}

		[TestCase]
		public void Clash_Double_Processing_Guard()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.OnClash(); // first call
			double afterFirst = (double)sim.Economy.Momentum;
			sim.OnClash(); // second call (same frame guard)
			double afterSecond = (double)sim.Economy.Momentum;
			// Guard should prevent double processing
			AssertThat(afterSecond).IsEqual(afterFirst);
		}

		[TestCase]
		public void ClashedThisFrame_Set_After_Clash()
		{
			var sim = CreateSim();
			sim.OnClash();
			AssertThat(sim.ClashedThisFrame).IsTrue();
		}

		[TestCase]
		public void ClashedThisFrame_Reset_On_Tick()
		{
			var sim = CreateSim();
			sim.OnClash();
			sim.Tick(EmptyInput()); // next tick clears
			AssertThat(sim.ClashedThisFrame).IsFalse();
		}

		// ══════════════════════════════════════════════════════════════
		//  DEATHBLOW
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Deathblow_On_Composure_Full()
		{
			var sim = CreateSim();
			// Push composure to 1.0
			for (int i = 0; i < 50; i++)
				sim.Economy.ApplyComposureDamage((Fixed64)0.5);
			AssertThat(sim.Economy.IsDeathblowVulnerable).IsTrue();
			// Any hit should trigger deathblow
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: false, AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.IsInDeathblow).IsTrue();
		}

		[TestCase]
		public void Deathblow_On_Vitality_Zero()
		{
			var sim = CreateSim();
			// Drain vitality to 0
			for (int i = 0; i < 50; i++)
				sim.Economy.ApplyVitalityDamage((Fixed64)0.5);
			AssertThat(sim.Economy.IsDeathblowVulnerable).IsTrue();
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: false, AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.IsInDeathblow).IsTrue();
		}

		[TestCase]
		public void Deathblow_Even_When_Blocked()
		{
			var sim = CreateSim();
			for (int i = 0; i < 50; i++)
				sim.Economy.ApplyComposureDamage((Fixed64)0.5);
			sim.Tick(BlockHeldInput()); // enter Blocking state (blocked hit requires blocker in Blocking)
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: true, AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.IsInDeathblow).IsTrue();
		}

		[TestCase]
		public void Deathblow_Is_Action_Locked()
		{
			var sim = CreateSim();
			sim.OnDeathblowTriggered();
			AssertThat(sim.IsActionLocked).IsTrue();
		}

		[TestCase]
		public void Deathblow_Stays_In_Deathblow()
		{
			var sim = CreateSim();
			sim.OnDeathblowTriggered();
			sim.Tick(EmptyInput());
			AssertThat(sim.IsInDeathblow).IsTrue();
		}

		[TestCase]
		public void OnDeathblowTriggered_Event()
		{
			var sim = CreateSim();
			sim.OnDeathblowTriggered();
			AssertThat(sim.LastEvent).IsEqual(CombatEvent.DeathblowTriggered);
		}

		[TestCase]
		public void Deathblow_Input_Ignored()
		{
			var sim = CreateSim();
			sim.OnDeathblowTriggered();
			sim.Tick(AttackInput());
			AssertThat(sim.IsInDeathblow).IsTrue();
		}

		[TestCase]
		public void Deathblow_Reset_Returns_To_Idle()
		{
			var sim = CreateSim();
			sim.OnDeathblowTriggered();
			sim.ResetState();
			sim.Tick(EmptyInput());
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ══════════════════════════════════════════════════════════════
		//  PARRY SUCCESS
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void OnParrySuccess_Costs_Momentum()
		{
			var sim = CreateSim();
			double before = (double)sim.Economy.Momentum;
			sim.OnParrySuccess();
			double after = (double)sim.Economy.Momentum;
			AssertThat(before - after).IsEqualApprox(0.5, 0.001);
		}

		[TestCase]
		public void OnParrySuccess_Grants_Stack()
		{
			var sim = CreateSim();
			sim.OnParrySuccess();
			AssertThat(sim.Economy.FrameAdvantageStacks).IsEqual(1);
		}

		[TestCase]
		public void OnParrySuccess_Sets_Event()
		{
			var sim = CreateSim();
			sim.OnParrySuccess();
			AssertThat(sim.LastEvent).IsEqual(CombatEvent.ParrySuccess);
		}

		[TestCase]
		public void Multiple_Parries_Accumulate_Stacks()
		{
			var sim = CreateSim();
			sim.OnParrySuccess();
			sim.OnParrySuccess();
			sim.OnParrySuccess();
			AssertThat(sim.Economy.FrameAdvantageStacks).IsEqual(3);
		}

		// ══════════════════════════════════════════════════════════════
		//  SHATTER WINDOW TIMING
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Shatter_Window_Active_For_Exactly_ParryFrames()
		{
			// §7.4: Window matches ParryWindowFrames (6)
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput()); // frame 0
											  // Frames 1-5: still in window (blockPressedFramesAgo = 1..5 < 6)
			for (int i = 0; i < 5; i++)
			{
				sim.Tick(EmptyInput());
				AssertThat(sim.IsInShatterWindow).IsTrue();
			}
			// Frame 6: window expired (blockPressedFramesAgo = 6 >= 6)
			sim.Tick(EmptyInput());
			AssertThat(sim.IsInShatterWindow).IsFalse();
		}

		[TestCase]
		public void Shatter_Window_Resets_On_New_Press()
		{
			var sim = CreateSim();
			sim.Tick(BlockParryPressInput());
			TickN(sim, 10); // expire window
			AssertThat(sim.IsInShatterWindow).IsFalse();

			// Re-enter actionable state and press again
			CompleteFullAttack(sim, AttackTier.Light); // return to Idle
			sim.Tick(BlockParryPressInput());
			AssertThat(sim.IsInShatterWindow).IsTrue();
		}

		// ══════════════════════════════════════════════════════════════
		//  DEATHBLOW + BLOCKED (§7.1 cannot prevent deathblow)
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Deathblow_On_Any_Tier_Hit()
		{
			// §11: "any strike lands" — T0 should also trigger
			var sim = CreateSim();
			for (int i = 0; i < 50; i++)
				sim.Economy.ApplyComposureDamage((Fixed64)0.5);
			sim.OnHitReceived((Fixed64)0.2, (Fixed64)0.1,
				wasBlocked: false, AttackTier.Light, staggerFrames: 0);
			AssertThat(sim.IsInDeathblow).IsTrue();
		}

		// ══════════════════════════════════════════════════════════════
		//  CLASH RECOVERY FRAME VALUES
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Clash_Recovery_Matches_Constant()
		{
			// §7.5: ClashRecoveryFrames = 8
			var sim = CreateSim();
			sim.OnClash();
			AssertThat(sim.DebugStateName).IsEqual("Recovery T0 [8f]");
		}

		// ══════════════════════════════════════════════════════════════
		//  PARRY DURING FATIGUE — falls back to blocking
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Parry_Unavailable_During_Fatigue_No_Momentum_Spent()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // fatigue
			double before = (double)sim.Economy.Momentum;
			sim.Tick(BlockParryPressInput());
			// Falls back to Blocking, no momentum spent
			AssertThat(sim.IsBlocking).IsTrue();
			AssertThat((double)sim.Economy.Momentum).IsEqual(before);
		}

		// ══════════════════════════════════════════════════════════════
		//  PREMATURE PRESS PENALTY — SHATTER INTERACTIONS (§7.6)
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Shatter_Whiff_Increments_Block_Penalty()
		{
			var sim = CreateSim();
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
			sim.OnShatterWhiff();
			AssertThat(sim.PrematureBlockPenalties).IsEqual(1);
		}

		[TestCase]
		public void Shatter_Success_Resets_Block_Penalty()
		{
			var sim = CreateSim();
			sim.OnShatterWhiff(); // penalty = 1
			sim.OnShatterLanded();
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
		}

		[TestCase]
		public void Penalty_Reduces_Shatter_Window()
		{
			var sim = CreateSim();
			sim.OnShatterWhiff(); // penalty = 1 → effective window = 3
			// Press block, immediate check (frame 0 after press)
			sim.Tick(BlockParryPressInput());
			AssertThat(sim.IsInShatterWindow).IsTrue();
			// Advance 3 frames total past the press (frame 3)
			TickN(sim, 3);
			// Effective window = 3, so frame index 3 is OUT of window
			AssertThat(sim.IsInShatterWindow).IsFalse();
		}

		[TestCase]
		public void Penalty_Shared_Between_Parry_Shatter()
		{
			var sim = CreateSim();
			// Whiff parry (penalty = 1)
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			sim.Tick(EmptyInput()); // exit blocking
			// Effective shatter window should also be 3
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(3);
		}

		[TestCase]
		public void No_Momentum_Gain_On_Shatter_Whiff()
		{
			var sim = CreateSim();
			double before = (double)sim.Economy.Momentum;
			sim.OnShatterWhiff();
			double after = (double)sim.Economy.Momentum;
			AssertThat(after).IsEqual(before);
		}

		[TestCase]
		public void Shatter_Whiff_Accumulates_With_Parry_Whiff()
		{
			var sim = CreateSim();
			// Parry whiff → penalty = 1
			sim.Tick(BlockParryPressInput());
			TickN(sim, 6);
			sim.Tick(EmptyInput());
			// Shatter whiff → penalty = 2
			sim.OnShatterWhiff();
			AssertThat(sim.PrematureBlockPenalties).IsEqual(2);
			AssertThat(sim.EffectiveParryWindowFrames).IsEqual(2);
		}

		[TestCase]
		public void Shatter_Window_Penalized_Exact_Frame_Count()
		{
			var sim = CreateSim();
			sim.OnShatterWhiff(); // penalty = 1 → effective window = 3
			// New block press
			sim.Tick(BlockParryPressInput()); // blockPressedFramesAgo = 0
			// Frames 1-2 should be in window
			sim.Tick(EmptyInput()); // ago = 1, 1 < 3 = true
			AssertThat(sim.IsInShatterWindow).IsTrue();
			sim.Tick(EmptyInput()); // ago = 2, 2 < 3 = true
			AssertThat(sim.IsInShatterWindow).IsTrue();
			// Frame 3: expired
			sim.Tick(EmptyInput()); // ago = 3, 3 < 3 = false
			AssertThat(sim.IsInShatterWindow).IsFalse();
		}

		[TestCase]
		public void Shatter_Penalty_Decays_After_Inactivity()
		{
			var sim = CreateSim();
			sim.OnShatterWhiff(); // penalty = 1
			AssertThat(sim.PrematureBlockPenalties).IsEqual(1);
			// No block press for 30 frames → inactivity reset
			TickN(sim, 30);
			AssertThat(sim.PrematureBlockPenalties).IsEqual(0);
			AssertThat(sim.IsInShatterWindow).IsFalse();
		}

		// ══════════════════════════════════════════════════════════════
		//  Shatter window initial state + OnClash from any state
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Initial_IsInShatterWindow_False()
		{
			// Before any block press, _blockPressedFramesAgo starts at int.MaxValue/2.
			// IsInShatterWindow is always false before first press.
			var sim = CreateSim();
			AssertThat(sim.IsInShatterWindow).IsFalse();
		}

		[TestCase]
		public void OnClash_From_Idle_Forces_Recovery()
		{
			// OnClash is called by HitboxManager after hit resolution regardless of state.
			// It must always put the sim into Recovery(ClashRecoveryFrames).
			var sim = CreateSim();
			AssertThat(sim.DebugStateName).IsEqual("Idle");
			sim.OnClash();
			AssertThat(sim.DebugStateName).Contains("Recovery");
		}

		[TestCase]
		public void OnClash_From_Blocking_Forces_Recovery()
		{
			var sim = CreateSim();
			sim.Tick(BlockHeldInput()); // enter Blocking
			AssertThat(sim.IsBlocking).IsTrue();
			sim.OnClash();
			AssertThat(sim.DebugStateName).Contains("Recovery");
		}
	}
}
