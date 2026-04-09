// Pure C# interface for archetype simulation data. No Godot imports.
using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	public interface IArchetypeData
	{
		int GetCoilFrames(AttackTier tier);
		int GetSwingFrames(AttackTier tier);
		int GetRecoveryFrames(AttackTier tier);
		(Fixed64 V, Fixed64 C) GetDamageMultipliers(AttackTier tier);
	}
}