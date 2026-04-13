using FixedMathSharp;

namespace ResonanceOfSteel.Simulation.Archetypes
{
	public sealed class LongswordData : IArchetypeData
	{
		public static readonly LongswordData Instance = new();
		private LongswordData() { }

		public MoveData GetMoveData(AttackTier tier) => tier switch
		{
			AttackTier.Light => new MoveData(
				coilFrames: 4, swingFrames: 2, recoveryFrames: 4,
				staggerFrames: 0, knockbackDistance: 0,
				vitalityMultiplier: (Fixed64)0.2, composureMultiplier: (Fixed64)0.1),

			AttackTier.Standard => new MoveData(
				coilFrames: 8, swingFrames: 4, recoveryFrames: 6,
				staggerFrames: 12, knockbackDistance: 2,
				vitalityMultiplier: Fixed64.One, composureMultiplier: Fixed64.One),

			AttackTier.Heavy => new MoveData(
				coilFrames: 16, swingFrames: 6, recoveryFrames: 10,
				staggerFrames: 18, knockbackDistance: 4,
				vitalityMultiplier: (Fixed64)1.5, composureMultiplier: (Fixed64)1.75),

			AttackTier.Super => new MoveData(
				coilFrames: 24, swingFrames: 8, recoveryFrames: 13,
				staggerFrames: 25, knockbackDistance: 6,
				vitalityMultiplier: (Fixed64)2.0, composureMultiplier: (Fixed64)2.0),

			_ => GetMoveData(AttackTier.Standard)
		};
	}
}