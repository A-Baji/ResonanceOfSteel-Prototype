using Godot;
using ResonanceOfSteel.Simulation;

namespace ResonanceOfSteel.Bridge.Archetypes
{
	public sealed class LongswordVisuals : IArchetypeVisuals
	{
		public static readonly LongswordVisuals Instance = new();
		private LongswordVisuals() { }
		// Pre-allocated shapes to avoid GC pressure during IntersectShape queries.
		private static readonly SphereShape3D FlickShape = new() { Radius = 0.4f };
		private static readonly CapsuleShape3D CrossCutShape = new() { Height = 1.2f, Radius = 0.2f };
		private static readonly CapsuleShape3D OverheadShape = new() { Height = 1.0f, Radius = 0.25f };
		private static readonly BoxShape3D LungeShape = new() { Size = new Vector3(1.8f, 0.2f, 0.2f) };

		public Shape3D GetHitboxShape(AttackTier tier) => tier switch
		{
			AttackTier.Light => FlickShape,
			AttackTier.Standard => CrossCutShape,
			AttackTier.Heavy => OverheadShape,
			AttackTier.Super => LungeShape,
			_ => FlickShape
		};
	}
}