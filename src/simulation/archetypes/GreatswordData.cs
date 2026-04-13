using FixedMathSharp;

namespace ResonanceOfSteel.Simulation.Archetypes
{
	public sealed class GreatswordData : IArchetypeData
	{
		public static readonly GreatswordData Instance = new();
		private GreatswordData() { }

		public MoveData GetMoveData(AttackTier tier) => tier switch
		{
			AttackTier.Light => new MoveData(
				coilFrames: 8, swingFrames: 3, recoveryFrames: 6,
				staggerFrames: 0, knockbackDistance: 0,
				vitalityMultiplier: (Fixed64)0.3, composureMultiplier: (Fixed64)0.15),

			AttackTier.Standard => new MoveData(
				coilFrames: 14, swingFrames: 6, recoveryFrames: 10,
				staggerFrames: 16, knockbackDistance: 3,
				vitalityMultiplier: (Fixed64)1.4, composureMultiplier: (Fixed64)1.2),

			AttackTier.Heavy => new MoveData(
				coilFrames: 24, swingFrames: 8, recoveryFrames: 16,
				staggerFrames: 22, knockbackDistance: 6,
				vitalityMultiplier: (Fixed64)2.0, composureMultiplier: (Fixed64)2.25),

			AttackTier.Super => new MoveData(
				coilFrames: 36, swingFrames: 10, recoveryFrames: 20,
				staggerFrames: 30, knockbackDistance: 8,
				vitalityMultiplier: (Fixed64)2.75, composureMultiplier: (Fixed64)2.75),

			_ => GetMoveData(AttackTier.Standard)
		};
	}
}