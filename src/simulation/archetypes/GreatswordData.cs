// src/simulation/Archetypes/LongswordData.cs
using FixedMathSharp;

namespace ResonanceOfSteel.Simulation.Archetypes
{
	public class GreatswordData : IArchetypeData
	{
		public int GetCoilFrames(AttackTier tier) => tier switch
		{
			AttackTier.Light => 8,
			AttackTier.Standard => 14,
			AttackTier.Heavy => 24,
			AttackTier.Super => 36,
			_ => 14
		};

		public int GetSwingFrames(AttackTier tier) => tier switch
		{
			AttackTier.Light => 3,
			AttackTier.Standard => 6,
			AttackTier.Heavy => 8,
			AttackTier.Super => 10,
			_ => 6
		};

		public int GetRecoveryFrames(AttackTier tier) => tier switch
		{
			AttackTier.Light => 6,
			AttackTier.Standard => 10,
			AttackTier.Heavy => 16,
			AttackTier.Super => 20,
			_ => 10
		};
		public (Fixed64 V, Fixed64 C) GetDamageMultipliers(AttackTier tier) => tier switch
		{
			AttackTier.Light => ((Fixed64)0.3f, (Fixed64)0.15f),
			AttackTier.Standard => ((Fixed64)1.4f, (Fixed64)1.2f),
			AttackTier.Heavy => ((Fixed64)2.0f, (Fixed64)2.25f),
			AttackTier.Super => ((Fixed64)2.75f, (Fixed64)2.75f),
			_ => (Fixed64.One, Fixed64.One)
		};
	}
}