using GdUnit4;
using static GdUnit4.Assertions;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;
using static ResonanceOfSteel.Tests.TestHelpers;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	public partial class AttackSystemTests
	{
		// ── Coil → Swing → Recovery → Idle cycle ──────────────────────

		[TestCase]
		public void Attack_Enters_Coil()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput());
			AssertThat(sim.DebugStateName).Contains("Coil");
		}

		[TestCase]
		public void Coil_Transitions_To_Swing()
		{
			var sim = CreateSim();
			AdvanceToSwing(sim, AttackTier.Standard);
			AssertThat(sim.DebugStateName).Contains("Swing");
		}

		[TestCase]
		public void Swing_Activates_Hitbox()
		{
			var sim = CreateSim();
			AdvanceToSwing(sim, AttackTier.Standard);
			// Tick once in Swing to set HitboxActive
			sim.Tick(EmptyInput());
			AssertThat(sim.HitboxActive).IsTrue();
		}

		[TestCase]
		public void Swing_Transitions_To_Recovery()
		{
			var sim = CreateSim();
			AdvanceToRecovery(sim, AttackTier.Standard);
			AssertThat(sim.DebugStateName).Contains("Recovery");
		}

		[TestCase]
		public void Recovery_Transitions_To_Idle()
		{
			var sim = CreateSim();
			CompleteFullAttack(sim, AttackTier.Standard);
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ── Longsword frame data ───────────────────────────────────────

		[TestCase]
		public void Longsword_T0_Coil_4_Frames()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput(AttackTier.Light));
			AssertThat(sim.DebugStateName).IsEqual("Coil T0 [3f]"); // 4-1=3 remaining
		}

		[TestCase]
		public void Longsword_T1_Coil_8_Frames()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput(AttackTier.Standard));
			AssertThat(sim.DebugStateName).IsEqual("Coil T1 [7f]");
		}

		[TestCase]
		public void Longsword_T2_Coil_16_Frames()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput(AttackTier.Heavy));
			AssertThat(sim.DebugStateName).IsEqual("Coil T2 [15f]");
		}

		[TestCase]
		public void Longsword_T3_Coil_24_Frames()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput(AttackTier.Super));
			AssertThat(sim.DebugStateName).IsEqual("Coil T3 [23f]");
		}

		// ── Greatsword frame data ──────────────────────────────────────

		[TestCase]
		public void Greatsword_T0_Coil_8_Frames()
		{
			var sim = CreateSim(GreatswordData.Instance);
			sim.Tick(AttackInput(AttackTier.Light));
			AssertThat(sim.DebugStateName).IsEqual("Coil T0 [7f]");
		}

		[TestCase]
		public void Greatsword_T1_Coil_14_Frames()
		{
			var sim = CreateSim(GreatswordData.Instance);
			sim.Tick(AttackInput(AttackTier.Standard));
			AssertThat(sim.DebugStateName).IsEqual("Coil T1 [13f]");
		}

		[TestCase]
		public void Greatsword_T2_Coil_24_Frames()
		{
			var sim = CreateSim(GreatswordData.Instance);
			sim.Tick(AttackInput(AttackTier.Heavy));
			AssertThat(sim.DebugStateName).IsEqual("Coil T2 [23f]");
		}

		[TestCase]
		public void Greatsword_T3_Coil_36_Frames()
		{
			var sim = CreateSim(GreatswordData.Instance);
			sim.Tick(AttackInput(AttackTier.Super));
			AssertThat(sim.DebugStateName).IsEqual("Coil T3 [35f]");
		}

		// ── Frame advantage coil reduction ─────────────────────────────

		[TestCase]
		public void Stacks_Reduce_Coil_Frames()
		{
			var sim = CreateSim();
			sim.Economy.IncrementFrameAdvantage();
			sim.Economy.IncrementFrameAdvantage(); // 2 stacks
			sim.Tick(AttackInput(AttackTier.Standard)); // T1: 8 coil - 2 = 6
			AssertThat(sim.DebugStateName).IsEqual("Coil T1 [5f]"); // 6-1=5 remaining
		}

		[TestCase]
		public void Stacks_Consumed_After_Coil_Reduction()
		{
			var sim = CreateSim();
			sim.Economy.IncrementFrameAdvantage();
			sim.Tick(AttackInput());
			AssertThat(sim.Economy.FrameAdvantageStacks).IsEqual(0);
		}

		[TestCase]
		public void Coil_Cannot_Be_Reduced_Below_One()
		{
			var sim = CreateSim();
			// Add many stacks to exceed coil frames
			for (int i = 0; i < 20; i++) sim.Economy.IncrementFrameAdvantage();
			sim.Tick(AttackInput(AttackTier.Light)); // T0 Longsword: 4 coil
			// Should be Coil with 0 remaining (1 frame minimum, consumed this tick)
			// Actually, Coil(1) → processed → 1-1=0 → transitions to Swing
			AssertThat(sim.DebugStateName).Contains("Swing"); // went straight through
		}

		// ── Tier 3 active frame armor ──────────────────────────────────

		[TestCase]
		public void T3_Armor_Active_During_Swing()
		{
			var sim = CreateSim();
			AdvanceToSwing(sim, AttackTier.Super);
			sim.Tick(EmptyInput()); // process swing
			AssertThat(sim.ArmorActive).IsTrue();
		}

		[TestCase]
		public void T3_Armor_Inactive_During_Coil()
		{
			var sim = CreateSim();
			sim.Tick(AttackInput(AttackTier.Super));
			AssertThat(sim.ArmorActive).IsFalse();
		}

		[TestCase]
		public void T3_Armor_Disabled_During_Fatigue()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0); // fatigue
			AdvanceToSwing(sim, AttackTier.Super);
			sim.Tick(EmptyInput());
			AssertThat(sim.ArmorActive).IsFalse();
		}

		[TestCase]
		public void Non_T3_No_Armor()
		{
			var sim = CreateSim();
			AdvanceToSwing(sim, AttackTier.Standard);
			sim.Tick(EmptyInput());
			AssertThat(sim.ArmorActive).IsFalse();
		}

		// ── T0 conditional damage ──────────────────────────────────────

		[TestCase]
		public void T0_Unblocked_Only_Vitality_Damage()
		{
			var sim = CreateSim();
			double vitBefore = (double)sim.Economy.Vitality;
			double compBefore = (double)sim.Economy.Composure;
			sim.OnHitReceived((Fixed64)0.2, (Fixed64)0.1,
				wasBlocked: false, AttackTier.Light, staggerFrames: 0);
			AssertThat((double)sim.Economy.Vitality).IsLess(vitBefore);
			AssertThat((double)sim.Economy.Composure).IsEqual(compBefore);
		}

		[TestCase]
		public void T0_Blocked_Only_Composure_Damage()
		{
			var sim = CreateSim();
			double vitBefore = (double)sim.Economy.Vitality;
			double compBefore = (double)sim.Economy.Composure;
			sim.OnHitReceived((Fixed64)0.2, (Fixed64)0.1,
				wasBlocked: true, AttackTier.Light, staggerFrames: 0);
			AssertThat((double)sim.Economy.Vitality).IsEqual(vitBefore);
			AssertThat((double)sim.Economy.Composure).IsGreater(compBefore);
		}

		// ── Blocked T1-T3 chip damage ──────────────────────────────────

		[TestCase]
		public void Blocked_T1_Deals_Chip_Vitality_And_Full_Composure()
		{
			var sim = CreateSim();
			double vitBefore = (double)sim.Economy.Vitality;
			double compBefore = (double)sim.Economy.Composure;
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: true, AttackTier.Standard, staggerFrames: 12);
			double chipDmg = vitBefore - (double)sim.Economy.Vitality;
			double fullDmg = 0.08; // BaseVitalityDamage × 1.0 × ChipMult(0.2)
			AssertThat(chipDmg).IsEqualApprox(fullDmg * 0.2, 0.001);
			AssertThat((double)sim.Economy.Composure).IsGreater(compBefore);
		}

		// ── Unblocked T1 full damage ───────────────────────────────────

		[TestCase]
		public void Unblocked_T1_Deals_Full_Vitality_And_Composure()
		{
			var sim = CreateSim();
			double vitBefore = (double)sim.Economy.Vitality;
			double compBefore = (double)sim.Economy.Composure;
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: false, AttackTier.Standard, staggerFrames: 12);
			double vitDmg = vitBefore - (double)sim.Economy.Vitality;
			AssertThat(vitDmg).IsEqualApprox(0.08, 0.001);
			AssertThat((double)sim.Economy.Composure).IsGreater(compBefore);
		}

		// ── Stagger from unblocked hit ─────────────────────────────────

		[TestCase]
		public void Unblocked_Hit_Causes_Stagger()
		{
			var sim = CreateSim();
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: false, AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.IsStaggered).IsTrue();
		}

		[TestCase]
		public void Blocked_Hit_Does_Not_Stagger()
		{
			var sim = CreateSim();
			sim.Tick(BlockHeldInput()); // enter blocking
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: true, AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.IsStaggered).IsFalse();
		}

		[TestCase]
		public void Stagger_Duration_Matches_MoveData()
		{
			var sim = CreateSim();
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: false, AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.DebugStateName).IsEqual("Staggered [12f]");
		}

		[TestCase]
		public void Stagger_Returns_To_Idle()
		{
			var sim = CreateSim();
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: false, AttackTier.Standard, staggerFrames: 5);
			TickN(sim, 5);
			AssertThat(sim.DebugStateName).IsEqual("Idle");
		}

		// ── Hit resets frame advantage ─────────────────────────────────

		[TestCase]
		public void Hit_Received_Resets_Stacks()
		{
			var sim = CreateSim();
			sim.Economy.IncrementFrameAdvantage();
			sim.Economy.IncrementFrameAdvantage();
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: false, AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.Economy.FrameAdvantageStacks).IsEqual(0);
		}

		[TestCase]
		public void Blocked_Hit_Resets_Stacks()
		{
			var sim = CreateSim();
			sim.Economy.IncrementFrameAdvantage();
			sim.OnHitReceived(Fixed64.One, Fixed64.One,
				wasBlocked: true, AttackTier.Standard, staggerFrames: 12);
			AssertThat(sim.Economy.FrameAdvantageStacks).IsEqual(0);
		}

		// ── Block does NOT reset stacks ────────────────────────────────

		[TestCase]
		public void Block_State_Preserves_Stacks()
		{
			var sim = CreateSim();
			sim.Economy.IncrementFrameAdvantage();
			sim.Tick(BlockHeldInput());
			AssertThat(sim.Economy.FrameAdvantageStacks).IsEqual(1);
		}

		// ── Hit landed generates momentum ──────────────────────────────

		[TestCase]
		public void OnHitLanded_Generates_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.OnHitLanded(Fixed64.One, Fixed64.One, wasBlocked: false);
			AssertThat((double)sim.Economy.Momentum).IsEqual(0.5);
		}

		[TestCase]
		public void OnHitLanded_Blocked_Also_Generates_Momentum()
		{
			var sim = CreateSim();
			sim.Economy.SpendMomentum((Fixed64)4.0);
			sim.OnHitLanded(Fixed64.One, Fixed64.One, wasBlocked: true);
			AssertThat((double)sim.Economy.Momentum).IsEqual(0.5);
		}

		// ── Recovery cannot be block-cancelled ─────────────────────────

		[TestCase]
		public void Recovery_Ignores_Block_Input()
		{
			var sim = CreateSim();
			AdvanceToRecovery(sim, AttackTier.Light);
			sim.Tick(BlockParryPressInput());
			AssertThat(sim.DebugStateName).Contains("Recovery");
		}
	}
}
