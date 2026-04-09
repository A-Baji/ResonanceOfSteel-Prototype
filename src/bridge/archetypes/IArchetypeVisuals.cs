// Godot-aware interface for archetype visuals and collision shapes.
using Godot;
using ResonanceOfSteel.Simulation;

namespace ResonanceOfSteel.Bridge
{
	public interface IArchetypeVisuals
	{
		Shape3D GetHitboxShape(AttackTier tier);
	}
}