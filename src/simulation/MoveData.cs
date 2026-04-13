using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	/// <summary>
	/// Complete frame and damage data for a single attack move.
	/// Returned by <see cref="IArchetypeData.GetMoveData"/> to avoid scattered queries.
	/// </summary>
	public readonly struct MoveData
	{
		public readonly int CoilFrames;
		public readonly int SwingFrames;
		public readonly int RecoveryFrames;
		public readonly int StaggerFrames;
		public readonly int KnockbackDistance;
		public readonly Fixed64 VitalityMultiplier;
		public readonly Fixed64 ComposureMultiplier;

		public MoveData(
			int coilFrames,
			int swingFrames,
			int recoveryFrames,
			int staggerFrames,
			int knockbackDistance,
			Fixed64 vitalityMultiplier,
			Fixed64 composureMultiplier)
		{
			CoilFrames = coilFrames;
			SwingFrames = swingFrames;
			RecoveryFrames = recoveryFrames;
			StaggerFrames = staggerFrames;
			KnockbackDistance = knockbackDistance;
			VitalityMultiplier = vitalityMultiplier;
			ComposureMultiplier = composureMultiplier;
		}
	}
}
