using GdUnit4;
using static GdUnit4.Assertions;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	public partial class EconomyHandlerTests
	{
		private EconomyConstants _c;
		private EconomyHandler _eco;

		[Before]
		public void Setup()
		{
			_c = EconomyConstants.Defaults;
			_eco = new EconomyHandler(_c);
		}

		// ── Momentum initial state ─────────────────────────────────────

		[TestCase]
		public void Initial_Momentum_Is_Half_Max()
		{
			AssertThat((double)_eco.Momentum).IsEqual(4.0);
		}

		[TestCase]
		public void Initial_Vitality_Is_One()
		{
			AssertThat((double)_eco.Vitality).IsEqual(1.0);
		}

		[TestCase]
		public void Initial_Composure_Is_Zero()
		{
			AssertThat((double)_eco.Composure).IsEqual(0.0);
		}

		[TestCase]
		public void Initial_Stacks_Is_Zero()
		{
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(0);
		}

		// ── Momentum spending ──────────────────────────────────────────

		[TestCase]
		public void CanAfford_True_When_Sufficient()
		{
			AssertThat(_eco.CanAfford((Fixed64)0.5)).IsTrue();
		}

		[TestCase]
		public void CanAfford_False_When_Insufficient()
		{
			_eco.SpendMomentum((Fixed64)4.0); // 0 left
			AssertThat(_eco.CanAfford((Fixed64)0.5)).IsFalse();
		}

		[TestCase]
		public void SpendMomentum_Clamps_At_Zero()
		{
			_eco.SpendMomentum((Fixed64)100.0);
			AssertThat((double)_eco.Momentum).IsEqual(0.0);
		}

		// ── Momentum generation ────────────────────────────────────────

		[TestCase]
		public void Walk_RoW_Generates_Momentum()
		{
			_eco.SpendMomentum((Fixed64)4.0); // start at 0
			_eco.AddRightOfWayMomentum(isRunning: false);
			AssertThat((double)_eco.Momentum).IsEqual(0.05);
		}

		[TestCase]
		public void Run_RoW_Generates_More_Momentum()
		{
			_eco.SpendMomentum((Fixed64)4.0);
			_eco.AddRightOfWayMomentum(isRunning: true);
			AssertThat((double)_eco.Momentum).IsEqual(0.1);
		}

		[TestCase]
		public void Momentum_Capped_At_Max()
		{
			for (int i = 0; i < 1000; i++)
				_eco.AddRightOfWayMomentum(isRunning: true);
			AssertThat((double)_eco.Momentum).IsEqual(8.0);
		}

		[TestCase]
		public void Hit_Generates_Momentum()
		{
			_eco.SpendMomentum((Fixed64)4.0);
			_eco.AddMomentumOnHit();
			AssertThat((double)_eco.Momentum).IsEqual(0.5);
		}

		[TestCase]
		public void Clash_Surge_Generates_Momentum()
		{
			_eco.SpendMomentum((Fixed64)4.0);
			_eco.AddClashSurge();
			AssertThat((double)_eco.Momentum).IsEqual(2.0);
		}

		// ── Fatigue ────────────────────────────────────────────────────

		[TestCase]
		public void IsFatigued_When_Momentum_Zero()
		{
			AssertThat(_eco.IsFatigued).IsFalse();
			_eco.SpendMomentum((Fixed64)4.0);
			AssertThat(_eco.IsFatigued).IsTrue();
		}

		// ── Vitality ───────────────────────────────────────────────────

		[TestCase]
		public void ApplyVitalityDamage_Reduces_Vitality()
		{
			_eco.ApplyVitalityDamage(Fixed64.One); // base × 1.0
			double expected = 1.0 - 0.08;
			AssertThat((double)_eco.Vitality).IsEqualApprox(expected, 0.001);
		}

		[TestCase]
		public void Vitality_Clamps_At_Zero()
		{
			_eco.ApplyVitalityDamage((Fixed64)100.0);
			AssertThat((double)_eco.Vitality).IsEqual(0.0);
		}

		[TestCase]
		public void IsTerminal_Below_Threshold()
		{
			_eco.ApplyVitalityDamage((Fixed64)12.0); // 0.08 × 12 = 0.96, vitality = 0.04
			AssertThat(_eco.IsTerminal).IsTrue();
		}

		[TestCase]
		public void IsTerminal_False_Above_Threshold()
		{
			AssertThat(_eco.IsTerminal).IsFalse();
		}

		// ── Composure ──────────────────────────────────────────────────

		[TestCase]
		public void ApplyComposureDamage_Increases_Composure()
		{
			_eco.ApplyComposureDamage(Fixed64.One);
			AssertThat((double)_eco.Composure).IsGreater(0.0);
		}

		[TestCase]
		public void Composure_Clamps_At_One()
		{
			_eco.ApplyComposureDamage((Fixed64)100.0);
			AssertThat((double)_eco.Composure).IsEqual(1.0);
		}

		[TestCase]
		public void IsDeathblowVulnerable_When_Composure_Full()
		{
			_eco.ApplyComposureDamage((Fixed64)100.0);
			AssertThat(_eco.IsDeathblowVulnerable).IsTrue();
		}

		[TestCase]
		public void IsDeathblowVulnerable_When_Vitality_Zero()
		{
			_eco.ApplyVitalityDamage((Fixed64)100.0);
			AssertThat(_eco.IsDeathblowVulnerable).IsTrue();
		}

		[TestCase]
		public void IsDeathblowVulnerable_False_When_Healthy()
		{
			AssertThat(_eco.IsDeathblowVulnerable).IsFalse();
		}

		// ── Composure recovery ────────────────────────────────────────

		[TestCase]
		public void Composure_Recovery_Paused_During_Cooldown()
		{
			_eco.ApplyComposureDamage(Fixed64.One);
			double composureAfterDamage = (double)_eco.Composure;
			// Tick 89 times (still in 90-frame cooldown)
			for (int i = 0; i < 89; i++)
				_eco.TickComposureRecovery();
			AssertThat((double)_eco.Composure).IsEqual(composureAfterDamage);
		}

		[TestCase]
		public void Composure_Recovery_Resumes_After_Cooldown()
		{
			_eco.ApplyComposureDamage(Fixed64.One);
			double composureAfterDamage = (double)_eco.Composure;
			for (int i = 0; i < 91; i++)
				_eco.TickComposureRecovery();
			AssertThat((double)_eco.Composure).IsLess(composureAfterDamage);
		}

		[TestCase]
		public void Composure_Recovery_Scales_With_Vitality_Squared()
		{
			_eco.ApplyComposureDamage((Fixed64)5.0);
			double fullHpComposure = (double)_eco.Composure;
			// Wait out cooldown + 10 recovery frames at full HP
			for (int i = 0; i < 100; i++) _eco.TickComposureRecovery();
			double recoveredAtFullHp = fullHpComposure - (double)_eco.Composure;

			// Reset and test at 50% HP
			_eco.FullReset();
			_eco.ApplyVitalityDamage((Fixed64)6.25); // 0.5 vitality
			_eco.ApplyComposureDamage((Fixed64)5.0);
			double halfHpComposure = (double)_eco.Composure;
			for (int i = 0; i < 100; i++) _eco.TickComposureRecovery();
			double recoveredAtHalfHp = halfHpComposure - (double)_eco.Composure;

			// At 50% HP, recovery should be ~25% of full HP rate (V² = 0.25)
			AssertThat(recoveredAtHalfHp).IsLess(recoveredAtFullHp);
		}

		[TestCase]
		public void Composure_Recovery_Halted_At_Terminal_Vitality()
		{
			_eco.ApplyVitalityDamage((Fixed64)12.0); // terminal
			_eco.ApplyComposureDamage(Fixed64.One);
			double composure = (double)_eco.Composure;
			for (int i = 0; i < 200; i++) _eco.TickComposureRecovery();
			AssertThat((double)_eco.Composure).IsEqual(composure);
		}

		// ── Frame Advantage Stacks ─────────────────────────────────────

		[TestCase]
		public void Parry_Increments_Stacks()
		{
			_eco.IncrementFrameAdvantage();
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(1);
			_eco.IncrementFrameAdvantage();
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(2);
		}

		[TestCase]
		public void Hit_Received_Resets_Stacks()
		{
			_eco.IncrementFrameAdvantage();
			_eco.IncrementFrameAdvantage();
			_eco.ResetFrameAdvantage();
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(0);
		}

		[TestCase]
		public void ConsumeCoilReduction_Returns_Stacks_Times_Offset()
		{
			_eco.IncrementFrameAdvantage();
			_eco.IncrementFrameAdvantage();
			_eco.IncrementFrameAdvantage();
			int reduction = _eco.ConsumeFrameAdvantageCoilReduction();
			AssertThat(reduction).IsEqual(3); // 3 stacks × 1 offset
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(0);
		}

		[TestCase]
		public void ConsumeCoilReduction_Returns_Zero_When_No_Stacks()
		{
			int reduction = _eco.ConsumeFrameAdvantageCoilReduction();
			AssertThat(reduction).IsEqual(0);
		}

		[TestCase]
		public void Stack_Decay_Starts_After_Delay()
		{
			_eco.IncrementFrameAdvantage();
			// Tick 179 frames (not yet at 180 delay)
			for (int i = 0; i < 179; i++) _eco.TickStackDecay();
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(1);
			// Frame 180 starts decay, but first decay at 180+60=240
			for (int i = 0; i < 61; i++) _eco.TickStackDecay();
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(0);
		}

		[TestCase]
		public void Stack_Decay_Loses_One_Per_Interval()
		{
			_eco.IncrementFrameAdvantage();
			_eco.IncrementFrameAdvantage();
			_eco.IncrementFrameAdvantage(); // 3 stacks
			// Wait past delay + 1 interval: 180 + 60 = 240 ticks
			for (int i = 0; i < 241; i++) _eco.TickStackDecay();
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(2);
		}

		// ── Full reset ─────────────────────────────────────────────────

		[TestCase]
		public void FullReset_Restores_All_Values()
		{
			_eco.SpendMomentum((Fixed64)4.0);
			_eco.ApplyVitalityDamage((Fixed64)5.0);
			_eco.ApplyComposureDamage((Fixed64)5.0);
			_eco.IncrementFrameAdvantage();
			_eco.FullReset();

			AssertThat((double)_eco.Momentum).IsEqual(4.0);
			AssertThat((double)_eco.Vitality).IsEqual(1.0);
			AssertThat((double)_eco.Composure).IsEqual(0.0);
			AssertThat(_eco.FrameAdvantageStacks).IsEqual(0);
		}

		// ── Chip damage ────────────────────────────────────────────────

		[TestCase]
		public void Chip_Damage_Is_20_Percent()
		{
			double chipMult = (double)(_c.ChipDamageMultiplier);
			AssertThat(chipMult).IsEqual(0.2);
		}
	}
}
