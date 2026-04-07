using Godot;
using ResonanceOfSteel.Simulation;

namespace ResonanceOfSteel.Bridge.Archetypes
{
	public class LongswordVisuals : IArchetypeVisuals
	{
		public Shape3D GetHitboxShape(AttackTier tier) => tier switch
		{
			AttackTier.Light => new SphereShape3D { Radius = 0.4f },
			AttackTier.Standard => new CapsuleShape3D { Height = 1.2f, Radius = 0.2f },
			AttackTier.Heavy => new CapsuleShape3D { Height = 1.0f, Radius = 0.25f },
			AttackTier.Super => new BoxShape3D { Size = new Vector3(1.8f, 0.2f, 0.2f) },
			_ => new SphereShape3D { Radius = 0.4f }
		};
	}
}