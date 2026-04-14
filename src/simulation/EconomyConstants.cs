// Default values from Prototype Brief Section 6 and Section 11.2.
// These are passed into EconomyHandler at construction time.
// The Bridge Layer will expose them as [Export] properties for inspector adjustment.
using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	public sealed record EconomyConstants(
		Fixed64 MomentumMax,               // 8.0 — maximum Momentum
		Fixed64 PerfectParryCost,           // 0.5 — Momentum cost per perfect parry
		Fixed64 DodgeCost,                  // 1.5 — Momentum cost per dodge
		Fixed64 JumpCost,                   // 1.0 — Momentum cost per jump
		Fixed64 ShatterCost,                // 3.0 — Momentum cost for Shatter modifier
		Fixed64 BaseMomentumOnHit,          // 0.5 — gained on any strike landing or being blocked
		Fixed64 RoWMomentumPerUnit,         // Momentum gained per unit distance moved toward opponent
		Fixed64 RetreatDrainPerUnit,        // Momentum lost per unit distance moved away from opponent
		Fixed64 RoWMaxRangeSquared,         // Squared max distance to opponent for RoW to apply
		Fixed64 ClashMomentumSurge,         // 2.0 — gained on Clash event
		Fixed64 ComposureBaseRecoveryRate,  // 0.0036 per frame (B_rate in the formula, 20% faster than Sekiro baseline)
		int ComposureRecoveryCooldownFrames, // 90 — frames after composure damage before recovery starts
		Fixed64 TerminalVitalityThreshold,  // 0.1 — below this, composure recovery = 0
		Fixed64 BaseVitalityDamage,         // Base damage value, multiplied by move multiplier
		Fixed64 BaseComposureDamage,        // Base composure damage, multiplied by move multiplier
		Fixed64 ChipDamageMultiplier,        // 0.2 — blocked T1-T3 attacks deal 20% vitality damage
		int FrameAdvantageOffset,           // 1 — frames subtracted from Coil per stack
		int StackDecayDelayFrames,          // 180 — frames after last parry before stacks start decaying
		int StackDecayIntervalFrames,       // 60 — frames between each stack loss during decay
		Fixed64 ArmorTradeLethality,        // 1.5 — Tier 3 armor trade incoming damage multiplier
		Fixed64 StaggerKnockbackMultiplier, // 0.2 — stagger pushback as fraction of block knockback
		int ParryWindowFrames,              // 6 — active parry window duration
		int DodgeStartupFrames,             // 3 — dodge startup (vulnerable)
		int DodgeActiveFrames,              // 12 — dodge active window (i-frames)
		int DodgeRecoveryFrames,            // 3 — dodge recovery (vulnerable)
		int JumpStartupFrames,              // 3 — jump startup (grounded, vulnerable)
		int JumpActiveFrames,               // 22 — jump active window (airborne)
		int JumpRecoveryFrames,             // 5 — jump recovery (landing, vulnerable)
		int EvasionFatigueStartupPenalty,   // 4 — extra startup frames when evasion is unaffordable
		int EvasionFatigueActiveReduction,  // 4 — active frames removed when evasion is unaffordable
		int ClashRecoveryFrames,            // 8 — recovery frames after a Clash event
		int ShatterWhiffPenaltyFrames,       // 20 — extra recovery frames on Shatter whiff
		int InputBufferTTL                    // 6 — input buffer time-to-live in frames
	)
	{
		public static EconomyConstants Defaults => new(
			MomentumMax: (Fixed64)8.0,
			PerfectParryCost: (Fixed64)0.5,
			DodgeCost: (Fixed64)1.5,
			JumpCost: (Fixed64)1.0,
			ShatterCost: (Fixed64)3.0,
			BaseMomentumOnHit: (Fixed64)0.5,
			RoWMomentumPerUnit: (Fixed64)0.375,
			RetreatDrainPerUnit: (Fixed64)0.1875,
			RoWMaxRangeSquared: (Fixed64)81.0,  // 9 units max range
			ClashMomentumSurge: (Fixed64)2.0,
			ComposureBaseRecoveryRate: (Fixed64)0.0036,
			ComposureRecoveryCooldownFrames: 90,
			TerminalVitalityThreshold: (Fixed64)0.1,
			BaseVitalityDamage: (Fixed64)0.08,
			BaseComposureDamage: (Fixed64)0.06,
			ChipDamageMultiplier: (Fixed64)0.2,
			FrameAdvantageOffset: 1,
			StackDecayDelayFrames: 180,
			StackDecayIntervalFrames: 60,
			ArmorTradeLethality: (Fixed64)1.5,
			StaggerKnockbackMultiplier: (Fixed64)0.2,
			ParryWindowFrames: 6,
			DodgeStartupFrames: 3,
			DodgeActiveFrames: 12,
			DodgeRecoveryFrames: 3,
			JumpStartupFrames: 3,
			JumpActiveFrames: 22,
			JumpRecoveryFrames: 5,
			EvasionFatigueStartupPenalty: 4,
			EvasionFatigueActiveReduction: 4,
			ClashRecoveryFrames: 8,
			ShatterWhiffPenaltyFrames: 20,
			InputBufferTTL: 6
		);
	}
}

