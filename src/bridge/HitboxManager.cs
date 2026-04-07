
// Manages manual physics queries for hit detection.
// Runs IntersectShape() each frame during active Swing states.
// See Prototype Brief Section 7.
using Godot;
using System.Collections.Generic;
using ResonanceOfSteel.Simulation;

namespace ResonanceOfSteel.Bridge
{
	public partial class HitboxManager : Node3D
	{
		// Set these from the parent scene.
		public PlayerBridge OwnerBridge { get; set; }
		public PlayerBridge OpponentBridge { get; set; }
		public IArchetypeVisuals ArchetypeVisuals { get; set; }
		public IArchetypeData ArchetypeData { get; set; }

		// Collision layers (Brief Section 7.2)
		private const uint HitboxLayer = 2;  // Layer 2: active during Swing
		private const uint HurtboxLayer = 4;  // Layer 3: active during Coil and Recovery

		// Frame counter to prevent registering the same hit twice in one Swing.
		private bool _hitRegisteredThisSwing = false;

		public void ProcessHitboxes()
		{
			if (OwnerBridge == null || OpponentBridge == null) return;

			if (!OwnerBridge.IsHitboxActive())
			{
				_hitRegisteredThisSwing = false;
				return;
			}

			if (_hitRegisteredThisSwing) return;

			var hit = QueryHitbox();
			if (hit) ResolveHit();
		}

		private bool QueryHitbox()
		{
			var spaceState = GetWorld3D().DirectSpaceState;
			var shape = ArchetypeVisuals.GetHitboxShape(OwnerBridge.GetCurrentTier());
			var myHurtbox = OwnerBridge.GetNode<Area3D>("Hurtbox");

			// Create a transform that is slightly in front of the player
			// This prevents hitting enemies standing behind you.
			Transform3D hitTransform = OwnerBridge.GlobalTransform;
			Vector3 forwardDirection = -hitTransform.Basis.Z; // Standard Godot forward
			hitTransform.Origin += forwardDirection * 1.0f; // Move hitbox 1 meter forward

			var query = new PhysicsShapeQueryParameters3D
			{
				Shape = shape,
				Transform = hitTransform, // Use the offset transform
				CollisionMask = HurtboxLayer,
				CollideWithAreas = true,
				CollideWithBodies = false,
				Exclude = new Godot.Collections.Array<Rid>
				{
					OwnerBridge.GetRid(), // Exclude the CharacterBody3D
					myHurtbox.GetRid()    // Exclude the Hurtbox Area3D
				}
			};

			var results = spaceState.IntersectShape(query);
			return results.Count > 0;
		}

		private void ResolveHit()
		{
			_hitRegisteredThisSwing = true;

			var tier = OwnerBridge.GetCurrentTier();
			var mults = ArchetypeData.GetDamageMultipliers(tier);

			// Determine if this is a block, parry, or clean hit.
			// Check opponent state name (exposed from simulation).
			string oppState = OpponentBridge.GetStateName();
			bool isBlocked = oppState == "Blocking" || oppState == "Parrying";
			bool isParried = oppState == "Parrying";

			if (isParried)
			{
				// Parry success — defender gets the benefit.
				// Check if it's a Shatter (attacker used Shatter modifier).
				// For now: treat as standard parry. Shatter added in Phase 6.
				OpponentBridge.NotifyParrySuccess();
				return;
			}

			// Apply damage to opponent.
			OpponentBridge.ReceiveHit(mults.V, mults.C, isBlocked);

			// Notify attacker that hit landed.
			OwnerBridge.NotifyHitLanded(mults.V, mults.C, isBlocked);
		}

		// Returns the hitbox shape for the current attack tier.
		// Shapes from Prototype Brief Section 7.1.
		// In Phase 6, this will be extended to support both archetypes.
		private Shape3D GetHitboxShape(AttackTier tier)
		{
			return tier switch
			{
				AttackTier.Light => new SphereShape3D { Radius = 0.4f },
				AttackTier.Standard => new CapsuleShape3D { Height = 1.2f, Radius = 0.2f },
				AttackTier.Heavy => new CapsuleShape3D { Height = 1.0f, Radius = 0.25f },
				AttackTier.Super => new BoxShape3D { Size = new Vector3(1.8f, 0.2f, 0.2f) },
				_ => new SphereShape3D { Radius = 0.4f },
			};
		}

		private (float v, float c) GetDamageMultipliers(AttackTier tier)
		{
			return tier switch
			{
				AttackTier.Light => (DamageMultipliers.Flick.V, DamageMultipliers.Flick.C),
				AttackTier.Standard => (DamageMultipliers.CrossCut.V, DamageMultipliers.CrossCut.C),
				AttackTier.Heavy => (DamageMultipliers.Overhead.V, DamageMultipliers.Overhead.C),
				AttackTier.Super => (DamageMultipliers.Lunge.V, DamageMultipliers.Lunge.C),
				_ => (1.0f, 1.0f),
			};
		}
	}
}

