using Godot;
using ResonanceOfSteel.Simulation;

namespace ResonanceOfSteel.Bridge.Archetypes
{
	public sealed class GreatswordVisuals : IArchetypeVisuals
	{
		public static readonly GreatswordVisuals Instance = new();
		private GreatswordVisuals() { }
		// Pre-allocated shapes to avoid GC pressure during IntersectShape queries.
		// Cleave uses a SphereShape3D at radius 1.5m as an approximation of a 180° swept arc.
		private static readonly SphereShape3D PommelShape = new() { Radius = 0.5f };
		private static readonly CapsuleShape3D WideSlashShape = new() { Height = 1.6f, Radius = 0.25f };
		private static readonly CapsuleShape3D CrushShape = new() { Height = 1.2f, Radius = 0.3f };
		private static readonly SphereShape3D CleaveShape = new() { Radius = 1.5f };

		public Shape3D GetHitboxShape(AttackTier tier) => tier switch
		{
			AttackTier.Light => PommelShape,
			AttackTier.Standard => WideSlashShape,
			AttackTier.Heavy => CrushShape,
			AttackTier.Super => CleaveShape,
			_ => PommelShape
		};
	}
}