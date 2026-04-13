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
			AssertThat(sim.DebugStateName).IsEqual("Recovery T1 [25f]"); // 26-1=25
		}

		[TestCase]
		public void ShatterWhiff_Penalty_Consumed_Once()
		{
			var sim = CreateSim();
			sim.OnShatterWhiff();
			CompleteFullAttack(sim, AttackTier.Standard);
			// Second attack should have normal recovery
			AdvanceToRecovery(sim, AttackTier.Standard);
			AssertThat(sim.DebugStateName).IsEqual("Recovery T1 [5f]"); // 6-1=5
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
	}
}
