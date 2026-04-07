using Godot;
using ResonanceOfSteel.Simulation;

namespace ResonanceOfSteel.Bridge.Archetypes
{
	public class GreatswordVisuals : IArchetypeVisuals
	{
		public Shape3D GetHitboxShape(AttackTier tier) => tier switch
		{
			AttackTier.Light => new SphereShape3D { Radius = 0.45f },
			AttackTier.Standard => new CapsuleShape3D { Height = 1.6f, Radius = 0.25f },
			AttackTier.Heavy => new CapsuleShape3D { Height = 1.2f, Radius = 0.35f },
			AttackTier.Super => new BoxShape3D { Size = new Vector3(2.2f, 0.3f, 0.3f) },
			_ => new SphereShape3D { Radius = 0.45f }
		};
	}
}