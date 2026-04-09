
// Default values from Prototype Brief Section 6 and Section 11.2.
// These are passed into EconomyHandler at construction time.
// The Bridge Layer will expose them as [Export] properties for inspector adjustment.
using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	public record EconomyConstants(
		Fixed64 MomentumMax,               // 8.0 - maximum Momentum
		Fixed64 PerfectParryCost,           // 0.5 - Momentum cost per perfect parry
		Fixed64 DodgeCost,                  // 1.5 - Momentum cost per dodge
		Fixed64 ShatterCost,                // 3.0 - Momentum cost for Shatter modifier
		Fixed64 BaseMomentumOnHit,          // 0.5 - gained on any strike landing or being blocked
		Fixed64 WalkMomentumRate,           // 0.05 per frame when walking toward opponent
		Fixed64 RunMomentumRate,            // 0.1 per frame when running toward opponent
		Fixed64 ClashMomentumSurge,         // 2.0 - gained on Clash event
		Fixed64 ComposureBaseRecoveryRate,  // 0.02 per frame (B_rate in the formula)
		Fixed64 TerminalVitalityThreshold,  // 0.1 - below this, composure recovery = 0
		Fixed64 BaseVitalityDamage,         // Base damage value, multiplied by move multiplier
		Fixed64 BaseComposureDamage,        // Base composure damage, multiplied by move multiplier
		int FrameAdvantageThreshold,        // 3 - stacks needed to gain frame advantage
		int FrameAdvantageOffset,           // 3 - frames subtracted from Coil on advantage
		Fixed64 ArmorTradeLethality,        // 1.5 - Tier 3 armor trade incoming damage multiplier
		Fixed64 FatigueRecoveryMultiplier   // 1.5 - recovery time multiplier when fatigued (Momentum at zero)
	)
	{
		// Factory method returning the defaults from the Prototype Brief.
		public static EconomyConstants Defaults => new(
			MomentumMax: (Fixed64)8.0,
			PerfectParryCost: (Fixed64)0.5,
			DodgeCost: (Fixed64)1.5,
			ShatterCost: (Fixed64)3.0,
			BaseMomentumOnHit: (Fixed64)0.5,
			WalkMomentumRate: (Fixed64)0.05,
			RunMomentumRate: (Fixed64)0.1,
			ClashMomentumSurge: (Fixed64)2.0,
			ComposureBaseRecoveryRate: (Fixed64)0.02,
			TerminalVitalityThreshold: (Fixed64)0.1,
			BaseVitalityDamage: (Fixed64)0.08,
			BaseComposureDamage: (Fixed64)0.06,
			FrameAdvantageThreshold: 3,
			FrameAdvantageOffset: 3,
			ArmorTradeLethality: (Fixed64)1.5,
			FatigueRecoveryMultiplier: (Fixed64)1.5
		);
	}
}

