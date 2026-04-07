// src/simulation/Archetypes/LongswordData.cs
using FixedMathSharp;

namespace ResonanceOfSteel.Simulation.Archetypes
{
	public class LongswordData : IArchetypeData
	{
		public int GetCoilFrames(AttackTier tier) => tier switch
		{
			AttackTier.Light => 4,
			AttackTier.Standard => 8,
			AttackTier.Heavy => 16,
			AttackTier.Super => 24,
			_ => 8
		};

		public int GetSwingFrames(AttackTier tier) => tier switch
		{
			AttackTier.Light => 2,
			AttackTier.Standard => 4,
			AttackTier.Heavy => 6,
			AttackTier.Super => 8,
			_ => 4
		};

		public int GetRecoveryFrames(AttackTier tier) => tier switch
		{
			AttackTier.Light => 4,
			AttackTier.Standard => 6,
			AttackTier.Heavy => 10,
			AttackTier.Super => 13,
			_ => 6
		};

		public (Fixed64 V, Fixed64 C) GetDamageMultipliers(AttackTier tier) => tier switch
		{
			AttackTier.Light => ((Fixed64)0.2f, (Fixed64)0.1f),
			AttackTier.Standard => ((Fixed64)1.0f, (Fixed64)1.0f),
			AttackTier.Heavy => ((Fixed64)1.5f, (Fixed64)1.75f),
			AttackTier.Super => ((Fixed64)2.0f, (Fixed64)2.0f),
			_ => (Fixed64.One, Fixed64.One)
		};
	}
}