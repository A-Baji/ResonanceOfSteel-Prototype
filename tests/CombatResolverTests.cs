// Tests for CombatResolver — the extracted pure-C# combat resolution logic.
// Every test maps to a specific CLAUDE.md spec section.
// Resolution order (§10.2): Clash → Armor → Parry+Shatter → Parry → ShatterWhiff → Deathblow → StandardHit
using GdUnit4;
using static GdUnit4.Assertions;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	public partial class CombatResolverTests
	{
		private static readonly Fixed64 DefaultArmorLethality = (Fixed64)1.5;

		// Helper: default attacker snapshot (swinging, not blocking/parrying)
		private static CombatSnapshot Attacker(AttackTier tier = AttackTier.Standard,
			bool inShatterWindow = false, bool clashedThisFrame = false) => new(
			isBlocking: false, isParrying: false, isInDeathblow: false,
			isArmorActive: false, isHitboxActive: true,
			isInShatterWindow: inShatterWindow,
			clashedThisFrame: clashedThisFrame, currentTier: tier);

		// Helper: default defender snapshot (idle, not doing anything)
		private static CombatSnapshot Defender(
			bool blocking = false, bool parrying = false, bool deathblow = false,
			bool armorActive = false, bool hitboxActive = false,
			AttackTier tier = AttackTier.Standard, bool clashedThisFrame = false) => new(
			isBlocking: blocking, isParrying: parrying, isInDeathblow: deathblow,
			isArmorActive: armorActive, isHitboxActive: hitboxActive,
			isInShatterWindow: false, clashedThisFrame: clashedThisFrame,
			currentTier: tier);

		private static MoveData T1Move => LongswordData.Instance.GetMoveData(AttackTier.Standard);
		private static MoveData T0Move => LongswordData.Instance.GetMoveData(AttackTier.Light);
		private static MoveData T3Move => LongswordData.Instance.GetMoveData(AttackTier.Super);

		// ══════════════════════════════════════════════════════════════
		//  §10.2 Resolution Order: Clash is checked FIRST
		// ══════════════════════════════════════════════════════════════

		/// <summary>§7.5: Same-tier simultaneous attacks → Clash</summary>
		[TestCase]
		public void SameTier_Both_Swinging_Is_Clash()
		{
			var result = CombatResolver.Resolve(
				Attacker(AttackTier.Standard),
				Defender(hitboxActive: true, tier: AttackTier.Standard),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsEqual(HitOutcome.Clash);
		}

		/// <summary>§7.5: Different tiers → NOT a Clash</summary>
		[TestCase]
		public void DifferentTier_Swinging_Is_Not_Clash()
		{
			var result = CombatResolver.Resolve(
				Attacker(AttackTier.Standard),
				Defender(hitboxActive: true, tier: AttackTier.Heavy),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsNotEqual(HitOutcome.Clash);
		}

		/// <summary>§7.5: Clash guard prevents double-processing</summary>
		[TestCase]
		public void Clash_Guard_Attacker_Already_Clashed()
		{
			var result = CombatResolver.Resolve(
				Attacker(AttackTier.Standard, clashedThisFrame: true),
				Defender(hitboxActive: true, tier: AttackTier.Standard),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsNotEqual(HitOutcome.Clash);
		}

		/// <summary>§7.5: Clash guard prevents double-processing (defender side)</summary>
		[TestCase]
		public void Clash_Guard_Defender_Already_Clashed()
		{
			var result = CombatResolver.Resolve(
				Attacker(AttackTier.Standard),
				Defender(hitboxActive: true, tier: AttackTier.Standard, clashedThisFrame: true),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsNotEqual(HitOutcome.Clash);
		}

		/// <summary>Defender not swinging → NOT a Clash</summary>
		[TestCase]
		public void Defender_Not_Swinging_Is_Not_Clash()
		{
			var result = CombatResolver.Resolve(
				Attacker(AttackTier.Standard),
				Defender(hitboxActive: false, tier: AttackTier.Standard),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsNotEqual(HitOutcome.Clash);
		}

		// ══════════════════════════════════════════════════════════════
		//  §6.2 Armor Trade (checked SECOND)
		// ══════════════════════════════════════════════════════════════

		/// <summary>§6.2: Defender with active T3 armor → ArmorTrade</summary>
		[TestCase]
		public void Defender_Armor_Active_Is_ArmorTrade()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(armorActive: true),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsEqual(HitOutcome.ArmorTrade);
		}

		/// <summary>§6.2: ArmorTrade applies 1.5× lethality multiplier</summary>
		[TestCase]
		public void ArmorTrade_Applies_Lethality_Multiplier()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(armorActive: true),
				T1Move, DefaultArmorLethality, false);
			double expected = (double)(T1Move.VitalityMultiplier * DefaultArmorLethality);
			AssertThat((double)result.VitalityMultiplier).IsEqualApprox(expected, 0.001);
		}

		/// <summary>§6.2: ArmorTrade does NOT stagger</summary>
		[TestCase]
		public void ArmorTrade_No_Stagger()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(armorActive: true),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.StaggerFrames).IsEqual(0);
		}

		// ══════════════════════════════════════════════════════════════
		//  §7.4 Shatter vs Parry (checked THIRD)
		// ══════════════════════════════════════════════════════════════

		/// <summary>§7.4: Attacker in shatter window + defender parrying + can afford → ShatterLanded</summary>
		[TestCase]
		public void Shatter_Into_Parry_Breaks_It()
		{
			var result = CombatResolver.Resolve(
				Attacker(inShatterWindow: true),
				Defender(parrying: true),
				T1Move, DefaultArmorLethality, attackerCanAffordShatter: true);
			AssertThat(result.Outcome).IsEqual(HitOutcome.ShatterLanded);
		}

		/// <summary>§7.4: ShatterLanded deals full damage + stagger</summary>
		[TestCase]
		public void ShatterLanded_Full_Damage()
		{
			var result = CombatResolver.Resolve(
				Attacker(inShatterWindow: true),
				Defender(parrying: true),
				T1Move, DefaultArmorLethality, attackerCanAffordShatter: true);
			AssertThat((double)result.VitalityMultiplier).IsEqualApprox(
				(double)T1Move.VitalityMultiplier, 0.001);
			AssertThat(result.StaggerFrames).IsEqual(T1Move.StaggerFrames);
		}

		/// <summary>§7.4: Attacker in shatter window but can't afford → normal ParrySuccess</summary>
		[TestCase]
		public void Shatter_Cant_Afford_Still_Parried()
		{
			var result = CombatResolver.Resolve(
				Attacker(inShatterWindow: true),
				Defender(parrying: true),
				T1Move, DefaultArmorLethality, attackerCanAffordShatter: false);
			AssertThat(result.Outcome).IsEqual(HitOutcome.ParrySuccess);
		}

		// ══════════════════════════════════════════════════════════════
		//  §7.2 Perfect Parry (checked FOURTH)
		// ══════════════════════════════════════════════════════════════

		/// <summary>§7.2: Defender parrying, attacker NOT shattering → ParrySuccess</summary>
		[TestCase]
		public void Parry_Without_Shatter_Is_ParrySuccess()
		{
			var result = CombatResolver.Resolve(
				Attacker(inShatterWindow: false),
				Defender(parrying: true),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsEqual(HitOutcome.ParrySuccess);
		}

		/// <summary>§7.2: ParrySuccess deals no damage</summary>
		[TestCase]
		public void ParrySuccess_No_Damage()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(parrying: true),
				T1Move, DefaultArmorLethality, false);
			AssertThat((double)result.VitalityMultiplier).IsEqual(0.0);
			AssertThat((double)result.ComposureMultiplier).IsEqual(0.0);
		}

		// ══════════════════════════════════════════════════════════════
		//  §7.4 Shatter Whiff (checked FIFTH)
		// ══════════════════════════════════════════════════════════════

		/// <summary>§7.4: Shatter window + can afford + defender NOT parrying → ShatterWhiff</summary>
		[TestCase]
		public void Shatter_Against_NonParrying_Is_Whiff()
		{
			var result = CombatResolver.Resolve(
				Attacker(inShatterWindow: true),
				Defender(blocking: false, parrying: false),
				T1Move, DefaultArmorLethality, attackerCanAffordShatter: true);
			AssertThat(result.Outcome).IsEqual(HitOutcome.ShatterWhiff);
		}

		/// <summary>§7.4: Shatter whiff against standard block → still whiff (no damage)</summary>
		[TestCase]
		public void Shatter_Against_Block_Is_Still_Whiff()
		{
			var result = CombatResolver.Resolve(
				Attacker(inShatterWindow: true),
				Defender(blocking: true),
				T1Move, DefaultArmorLethality, attackerCanAffordShatter: true);
			AssertThat(result.Outcome).IsEqual(HitOutcome.ShatterWhiff);
		}

		/// <summary>§7.4: ShatterWhiff deals no damage</summary>
		[TestCase]
		public void ShatterWhiff_No_Damage()
		{
			var result = CombatResolver.Resolve(
				Attacker(inShatterWindow: true),
				Defender(),
				T1Move, DefaultArmorLethality, attackerCanAffordShatter: true);
			AssertThat((double)result.VitalityMultiplier).IsEqual(0.0);
			AssertThat((double)result.ComposureMultiplier).IsEqual(0.0);
		}

		// ══════════════════════════════════════════════════════════════
		//  §11 Deathblow (checked SIXTH)
		// ══════════════════════════════════════════════════════════════

		/// <summary>§7.1: Defender already in Deathblow → execution</summary>
		[TestCase]
		public void Hit_Deathblow_Defender_Triggers_Execution()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(deathblow: true),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsEqual(HitOutcome.Deathblow);
		}

		/// <summary>§7.1: "Cannot prevent Deathblow" — blocking doesn't help</summary>
		[TestCase]
		public void Deathblow_Not_Prevented_By_Block()
		{
			// Deathblow state is checked AFTER shatter/parry, but the defender
			// is not parrying (just blocking), so it falls through to Deathblow.
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(deathblow: true, blocking: true),
				T1Move, DefaultArmorLethality, false);
			// Blocking + deathblow → defender.IsParrying=false, so it doesn't hit
			// the Parry branch. Shatter window is false. It reaches Deathblow check.
			AssertThat(result.Outcome).IsEqual(HitOutcome.Deathblow);
		}

		// ══════════════════════════════════════════════════════════════
		//  §7.1 Standard Block / Hit (checked LAST)
		// ══════════════════════════════════════════════════════════════

		/// <summary>§7.1: Standard Block — defender blocking, not parrying</summary>
		[TestCase]
		public void StandardBlock_Returns_Blocked()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(blocking: true),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsEqual(HitOutcome.Blocked);
		}

		/// <summary>§7.1: Unblocked hit → Hit</summary>
		[TestCase]
		public void Unblocked_Returns_Hit()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsEqual(HitOutcome.Hit);
		}

		/// <summary>§7.1: Hit carries full damage multipliers from MoveData</summary>
		[TestCase]
		public void Hit_Carries_Full_Multipliers()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(),
				T1Move, DefaultArmorLethality, false);
			AssertThat((double)result.VitalityMultiplier).IsEqualApprox(
				(double)T1Move.VitalityMultiplier, 0.001);
			AssertThat((double)result.ComposureMultiplier).IsEqualApprox(
				(double)T1Move.ComposureMultiplier, 0.001);
		}

		/// <summary>§7.1: Hit includes stagger frames from MoveData</summary>
		[TestCase]
		public void Hit_Includes_Stagger_Frames()
		{
			var result = CombatResolver.Resolve(
				Attacker(),
				Defender(),
				T1Move, DefaultArmorLethality, false);
			AssertThat(result.StaggerFrames).IsEqual(T1Move.StaggerFrames);
		}

		// ══════════════════════════════════════════════════════════════
		//  §6.1 T0 special rules (no stagger)
		// ══════════════════════════════════════════════════════════════

		/// <summary>§6.1: T0 has StaggerFrames = 0</summary>
		[TestCase]
		public void T0_No_Stagger_In_MoveData()
		{
			var result = CombatResolver.Resolve(
				Attacker(AttackTier.Light),
				Defender(),
				T0Move, DefaultArmorLethality, false);
			AssertThat(result.StaggerFrames).IsEqual(0);
		}

		// ══════════════════════════════════════════════════════════════
		//  Resolution priority edge cases
		// ══════════════════════════════════════════════════════════════

		/// <summary>Clash takes priority over everything (armor, parry, block)</summary>
		[TestCase]
		public void Clash_Priority_Over_Armor()
		{
			var result = CombatResolver.Resolve(
				Attacker(AttackTier.Super),
				Defender(armorActive: true, hitboxActive: true, tier: AttackTier.Super),
				T3Move, DefaultArmorLethality, false);
			AssertThat(result.Outcome).IsEqual(HitOutcome.Clash);
		}

		/// <summary>Shatter whiff takes priority over deathblow (§7.4)</summary>
		[TestCase]
		public void ShatterWhiff_Priority_Over_Deathblow()
		{
			var result = CombatResolver.Resolve(
				Attacker(inShatterWindow: true),
				Defender(deathblow: true),
				T1Move, DefaultArmorLethality, attackerCanAffordShatter: true);
			AssertThat(result.Outcome).IsEqual(HitOutcome.ShatterWhiff);
		}
	}
}
