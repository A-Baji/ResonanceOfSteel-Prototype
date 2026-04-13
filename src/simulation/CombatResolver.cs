// Pure C# combat resolution logic extracted from HitboxManager.
// Determines the outcome of a hit based on attacker/defender state.
// No Godot imports. Testable in isolation.
//
// The Bridge's HitboxManager calls this after IntersectShape() confirms a hit,
// then applies the CombatResult to both PlayerSimulations.
using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	/// <summary>
	/// The possible outcomes of a hit connecting.
	/// </summary>
	public enum HitOutcome
	{
		/// <summary>Same-tier simultaneous strikes. Both get momentum surge + knockback.</summary>
		Clash,
		/// <summary>Defender has Tier 3 armor active. Hit lands with ArmorTradeLethality multiplier, no stagger.</summary>
		ArmorTrade,
		/// <summary>Attacker's Shatter breaks defender's parry. Full damage + stagger.</summary>
		ShatterLanded,
		/// <summary>Defender successfully Perfect Parried (attacker was not Shattering).</summary>
		ParrySuccess,
		/// <summary>Attacker committed to Shatter but defender was NOT parrying. No damage, penalty recovery.</summary>
		ShatterWhiff,
		/// <summary>Defender is in Deathblow state. Execution triggered.</summary>
		Deathblow,
		/// <summary>Standard blocked hit. Chip damage + composure.</summary>
		Blocked,
		/// <summary>Standard unblocked hit. Full damage + stagger.</summary>
		Hit,
	}

	/// <summary>
	/// Full result of combat resolution. Produced by CombatResolver, consumed by Bridge.
	/// </summary>
	public readonly struct CombatResult
	{
		public readonly HitOutcome Outcome;
		public readonly AttackTier AttackerTier;
		public readonly Fixed64 VitalityMultiplier;
		public readonly Fixed64 ComposureMultiplier;
		public readonly int StaggerFrames;
		public readonly int KnockbackDistance;

		public CombatResult(HitOutcome outcome, AttackTier attackerTier,
			Fixed64 vitalityMult, Fixed64 composureMult, int staggerFrames, int knockbackDistance)
		{
			Outcome = outcome;
			AttackerTier = attackerTier;
			VitalityMultiplier = vitalityMult;
			ComposureMultiplier = composureMult;
			StaggerFrames = staggerFrames;
			KnockbackDistance = knockbackDistance;
		}
	}

	/// <summary>
	/// Snapshot of a player's combat-relevant state at the moment of hit resolution.
	/// Built by the Bridge from PlayerSimulation + PlayerBridge queries.
	/// </summary>
	public readonly struct CombatSnapshot
	{
		public readonly bool IsBlocking;
		public readonly bool IsParrying;
		public readonly bool IsInDeathblow;
		public readonly bool IsArmorActive;
		public readonly bool IsHitboxActive;
		public readonly bool IsInShatterWindow;
		public readonly bool ClashedThisFrame;
		public readonly AttackTier CurrentTier;

		public CombatSnapshot(bool isBlocking, bool isParrying, bool isInDeathblow,
			bool isArmorActive, bool isHitboxActive, bool isInShatterWindow,
			bool clashedThisFrame, AttackTier currentTier)
		{
			IsBlocking = isBlocking;
			IsParrying = isParrying;
			IsInDeathblow = isInDeathblow;
			IsArmorActive = isArmorActive;
			IsHitboxActive = isHitboxActive;
			IsInShatterWindow = isInShatterWindow;
			ClashedThisFrame = clashedThisFrame;
			CurrentTier = currentTier;
		}
	}

	/// <summary>
	/// Determines combat outcome from attacker/defender state snapshots.
	/// Resolution order (CLAUDE.md §10.2):
	///   Clash → Armor → Parry+Shatter → Parry → ShatterWhiff → Deathblow → StandardHit
	/// </summary>
	public static class CombatResolver
	{
		public static CombatResult Resolve(
			CombatSnapshot attacker,
			CombatSnapshot defender,
			MoveData move,
			Fixed64 armorTradeLethality,
			bool attackerCanAffordShatter)
		{
			// ── Clash: same-tier simultaneous swings ────────────────────
			if (!attacker.ClashedThisFrame && !defender.ClashedThisFrame
				&& defender.IsHitboxActive && defender.CurrentTier == attacker.CurrentTier)
			{
				return new CombatResult(HitOutcome.Clash, attacker.CurrentTier,
					move.VitalityMultiplier, move.ComposureMultiplier,
					0, move.KnockbackDistance);
			}

			// ── Armor trade: defender in T3 Swing with active armor ─────
			if (defender.IsArmorActive)
			{
				var armorVMult = move.VitalityMultiplier * armorTradeLethality;
				return new CombatResult(HitOutcome.ArmorTrade, attacker.CurrentTier,
					armorVMult, move.ComposureMultiplier, 0, 0);
			}

			// ── Parry / Shatter resolution ──────────────────────────────
			if (defender.IsParrying)
			{
				bool isShatter = attacker.IsInShatterWindow && attackerCanAffordShatter;
				if (isShatter)
				{
					return new CombatResult(HitOutcome.ShatterLanded, attacker.CurrentTier,
						move.VitalityMultiplier, move.ComposureMultiplier,
						move.StaggerFrames, move.KnockbackDistance);
				}
				return new CombatResult(HitOutcome.ParrySuccess, attacker.CurrentTier,
					Fixed64.Zero, Fixed64.Zero, 0, 0);
			}

			// ── Shatter whiff: attacker in shatter window but defender NOT parrying
			if (attacker.IsInShatterWindow && attackerCanAffordShatter)
			{
				return new CombatResult(HitOutcome.ShatterWhiff, attacker.CurrentTier,
					Fixed64.Zero, Fixed64.Zero, 0, 0);
			}

			// ── Deathblow execution ─────────────────────────────────────
			if (defender.IsInDeathblow)
			{
				return new CombatResult(HitOutcome.Deathblow, attacker.CurrentTier,
					move.VitalityMultiplier, move.ComposureMultiplier,
					move.StaggerFrames, move.KnockbackDistance);
			}

			// ── Standard hit / block ────────────────────────────────────
			bool isBlocked = defender.IsBlocking || defender.IsParrying;
			return new CombatResult(
				isBlocked ? HitOutcome.Blocked : HitOutcome.Hit,
				attacker.CurrentTier,
				move.VitalityMultiplier, move.ComposureMultiplier,
				move.StaggerFrames, move.KnockbackDistance);
		}
	}
}
