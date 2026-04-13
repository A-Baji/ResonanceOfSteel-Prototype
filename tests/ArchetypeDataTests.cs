using GdUnit4;
using static GdUnit4.Assertions;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	public partial class ArchetypeDataTests
	{
		// ══════════════════════════════════════════════════════════════
		//  LONGSWORD frame data verification (from CLAUDE.md §8.1)
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Longsword_Flick_FrameData()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Light);
			AssertThat(m.CoilFrames).IsEqual(4);
			AssertThat(m.SwingFrames).IsEqual(2);
			AssertThat(m.RecoveryFrames).IsEqual(4);
		}

		[TestCase]
		public void Longsword_CrossCut_FrameData()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat(m.CoilFrames).IsEqual(8);
			AssertThat(m.SwingFrames).IsEqual(4);
			AssertThat(m.RecoveryFrames).IsEqual(6);
		}

		[TestCase]
		public void Longsword_Overhead_FrameData()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Heavy);
			AssertThat(m.CoilFrames).IsEqual(16);
			AssertThat(m.SwingFrames).IsEqual(6);
			AssertThat(m.RecoveryFrames).IsEqual(10);
		}

		[TestCase]
		public void Longsword_Lunge_FrameData()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Super);
			AssertThat(m.CoilFrames).IsEqual(24);
			AssertThat(m.SwingFrames).IsEqual(8);
			AssertThat(m.RecoveryFrames).IsEqual(13);
		}

		[TestCase]
		public void Longsword_Flick_TotalFrames_10()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Light);
			AssertThat(m.CoilFrames + m.SwingFrames + m.RecoveryFrames).IsEqual(10);
		}

		[TestCase]
		public void Longsword_CrossCut_TotalFrames_18()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat(m.CoilFrames + m.SwingFrames + m.RecoveryFrames).IsEqual(18);
		}

		[TestCase]
		public void Longsword_Overhead_TotalFrames_32()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Heavy);
			AssertThat(m.CoilFrames + m.SwingFrames + m.RecoveryFrames).IsEqual(32);
		}

		[TestCase]
		public void Longsword_Lunge_TotalFrames_45()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Super);
			AssertThat(m.CoilFrames + m.SwingFrames + m.RecoveryFrames).IsEqual(45);
		}

		// ── Longsword damage multipliers ───────────────────────────────

		[TestCase]
		public void Longsword_Flick_DamageMultipliers()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Light);
			AssertThat((double)m.VitalityMultiplier).IsEqualApprox(0.2, 0.001);
			AssertThat((double)m.ComposureMultiplier).IsEqualApprox(0.1, 0.001);
		}

		[TestCase]
		public void Longsword_CrossCut_DamageMultipliers()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat((double)m.VitalityMultiplier).IsEqualApprox(1.0, 0.001);
			AssertThat((double)m.ComposureMultiplier).IsEqualApprox(1.0, 0.001);
		}

		[TestCase]
		public void Longsword_Overhead_DamageMultipliers()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Heavy);
			AssertThat((double)m.VitalityMultiplier).IsEqualApprox(1.5, 0.001);
			AssertThat((double)m.ComposureMultiplier).IsEqualApprox(1.75, 0.001);
		}

		[TestCase]
		public void Longsword_Lunge_DamageMultipliers()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Super);
			AssertThat((double)m.VitalityMultiplier).IsEqualApprox(2.0, 0.001);
			AssertThat((double)m.ComposureMultiplier).IsEqualApprox(2.0, 0.001);
		}

		// ── Longsword stagger/knockback ────────────────────────────────

		[TestCase]
		public void Longsword_T0_No_Stagger()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Light);
			AssertThat(m.StaggerFrames).IsEqual(0);
			AssertThat((double)m.KnockbackDistance).IsEqual(0.0);
		}

		[TestCase]
		public void Longsword_T1_Stagger_12()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat(m.StaggerFrames).IsEqual(12);
		}

		[TestCase]
		public void Longsword_T2_Stagger_18()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Heavy);
			AssertThat(m.StaggerFrames).IsEqual(18);
		}

		[TestCase]
		public void Longsword_T3_Stagger_25()
		{
			var m = LongswordData.Instance.GetMoveData(AttackTier.Super);
			AssertThat(m.StaggerFrames).IsEqual(25);
		}

		// ── Longsword singleton ────────────────────────────────────────

		[TestCase]
		public void Longsword_Singleton_Same_Instance()
		{
			AssertThat(LongswordData.Instance).IsSame(LongswordData.Instance);
		}

		// ── Longsword fallback ─────────────────────────────────────────

		[TestCase]
		public void Longsword_Invalid_Tier_Falls_Back_To_Standard()
		{
			var fallback = LongswordData.Instance.GetMoveData((AttackTier)99);
			var standard = LongswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat(fallback.CoilFrames).IsEqual(standard.CoilFrames);
			AssertThat(fallback.SwingFrames).IsEqual(standard.SwingFrames);
		}

		// ══════════════════════════════════════════════════════════════
		//  GREATSWORD frame data verification (from CLAUDE.md §8.2)
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Greatsword_PommelStrike_FrameData()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Light);
			AssertThat(m.CoilFrames).IsEqual(8);
			AssertThat(m.SwingFrames).IsEqual(3);
			AssertThat(m.RecoveryFrames).IsEqual(6);
		}

		[TestCase]
		public void Greatsword_WideSlash_FrameData()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat(m.CoilFrames).IsEqual(14);
			AssertThat(m.SwingFrames).IsEqual(6);
			AssertThat(m.RecoveryFrames).IsEqual(10);
		}

		[TestCase]
		public void Greatsword_Crush_FrameData()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Heavy);
			AssertThat(m.CoilFrames).IsEqual(24);
			AssertThat(m.SwingFrames).IsEqual(8);
			AssertThat(m.RecoveryFrames).IsEqual(16);
		}

		[TestCase]
		public void Greatsword_Cleave_FrameData()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Super);
			AssertThat(m.CoilFrames).IsEqual(36);
			AssertThat(m.SwingFrames).IsEqual(10);
			AssertThat(m.RecoveryFrames).IsEqual(20);
		}

		[TestCase]
		public void Greatsword_PommelStrike_TotalFrames_17()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Light);
			AssertThat(m.CoilFrames + m.SwingFrames + m.RecoveryFrames).IsEqual(17);
		}

		[TestCase]
		public void Greatsword_WideSlash_TotalFrames_30()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat(m.CoilFrames + m.SwingFrames + m.RecoveryFrames).IsEqual(30);
		}

		[TestCase]
		public void Greatsword_Crush_TotalFrames_48()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Heavy);
			AssertThat(m.CoilFrames + m.SwingFrames + m.RecoveryFrames).IsEqual(48);
		}

		[TestCase]
		public void Greatsword_Cleave_TotalFrames_66()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Super);
			AssertThat(m.CoilFrames + m.SwingFrames + m.RecoveryFrames).IsEqual(66);
		}

		// ── Greatsword damage multipliers ──────────────────────────────

		[TestCase]
		public void Greatsword_PommelStrike_DamageMultipliers()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Light);
			AssertThat((double)m.VitalityMultiplier).IsEqualApprox(0.3, 0.001);
			AssertThat((double)m.ComposureMultiplier).IsEqualApprox(0.15, 0.001);
		}

		[TestCase]
		public void Greatsword_WideSlash_DamageMultipliers()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat((double)m.VitalityMultiplier).IsEqualApprox(1.4, 0.001);
			AssertThat((double)m.ComposureMultiplier).IsEqualApprox(1.2, 0.001);
		}

		[TestCase]
		public void Greatsword_Crush_DamageMultipliers()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Heavy);
			AssertThat((double)m.VitalityMultiplier).IsEqualApprox(2.0, 0.001);
			AssertThat((double)m.ComposureMultiplier).IsEqualApprox(2.25, 0.001);
		}

		[TestCase]
		public void Greatsword_Cleave_DamageMultipliers()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Super);
			AssertThat((double)m.VitalityMultiplier).IsEqualApprox(2.75, 0.001);
			AssertThat((double)m.ComposureMultiplier).IsEqualApprox(2.75, 0.001);
		}

		// ── Greatsword stagger/knockback ───────────────────────────────

		[TestCase]
		public void Greatsword_T0_No_Stagger()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Light);
			AssertThat(m.StaggerFrames).IsEqual(0);
			AssertThat((double)m.KnockbackDistance).IsEqual(0.0);
		}

		[TestCase]
		public void Greatsword_T1_Stagger_16()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat(m.StaggerFrames).IsEqual(16);
		}

		[TestCase]
		public void Greatsword_T2_Stagger_22()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Heavy);
			AssertThat(m.StaggerFrames).IsEqual(22);
		}

		[TestCase]
		public void Greatsword_T3_Stagger_30()
		{
			var m = GreatswordData.Instance.GetMoveData(AttackTier.Super);
			AssertThat(m.StaggerFrames).IsEqual(30);
		}

		// ── Greatsword singleton ───────────────────────────────────────

		[TestCase]
		public void Greatsword_Singleton_Same_Instance()
		{
			AssertThat(GreatswordData.Instance).IsSame(GreatswordData.Instance);
		}

		// ── Greatsword fallback ────────────────────────────────────────

		[TestCase]
		public void Greatsword_Invalid_Tier_Falls_Back_To_Standard()
		{
			var fallback = GreatswordData.Instance.GetMoveData((AttackTier)99);
			var standard = GreatswordData.Instance.GetMoveData(AttackTier.Standard);
			AssertThat(fallback.CoilFrames).IsEqual(standard.CoilFrames);
		}

		// ══════════════════════════════════════════════════════════════
		//  CROSS-ARCHETYPE comparisons (design invariants from §16)
		// ══════════════════════════════════════════════════════════════

		[TestCase]
		public void Greatsword_Always_Slower_Than_Longsword()
		{
			foreach (AttackTier tier in new[] { AttackTier.Light, AttackTier.Standard, AttackTier.Heavy, AttackTier.Super })
			{
				var ls = LongswordData.Instance.GetMoveData(tier);
				var gs = GreatswordData.Instance.GetMoveData(tier);
				int lsTotal = ls.CoilFrames + ls.SwingFrames + ls.RecoveryFrames;
				int gsTotal = gs.CoilFrames + gs.SwingFrames + gs.RecoveryFrames;
				AssertThat(gsTotal).IsGreater(lsTotal);
			}
		}

		[TestCase]
		public void Greatsword_Hits_Harder_Than_Longsword()
		{
			foreach (AttackTier tier in new[] { AttackTier.Light, AttackTier.Standard, AttackTier.Heavy, AttackTier.Super })
			{
				var ls = LongswordData.Instance.GetMoveData(tier);
				var gs = GreatswordData.Instance.GetMoveData(tier);
				AssertThat((double)gs.VitalityMultiplier).IsGreaterEqual((double)ls.VitalityMultiplier);
			}
		}

		[TestCase]
		public void Greatsword_Longer_Stagger_Than_Longsword()
		{
			foreach (AttackTier tier in new[] { AttackTier.Standard, AttackTier.Heavy, AttackTier.Super })
			{
				var ls = LongswordData.Instance.GetMoveData(tier);
				var gs = GreatswordData.Instance.GetMoveData(tier);
				AssertThat(gs.StaggerFrames).IsGreater(ls.StaggerFrames);
			}
		}
	}
}
