// Pure C# interface for archetype simulation data. No Godot imports.

namespace ResonanceOfSteel.Simulation.Archetypes
{
	public interface IArchetypeData
	{
		MoveData GetMoveData(AttackTier tier);
	}
}