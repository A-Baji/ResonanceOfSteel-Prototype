
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
			bool isDeathblow = oppState == "Deathblow";
			bool attackerAttemptingShatter = OwnerBridge.IsInShatterWindow();

			if (isParried)
			{
				// Shatter: the attacker pressed block within the parry window of this contact
				// frame (opponent being in Parrying already guarantees their side), and the
				// attacker can afford the Momentum cost.
				bool isShatter = attackerAttemptingShatter && OwnerBridge.TryInitiateShatter();

				if (isShatter)
				{
					// Break the parry — deal full unblocked damage to the defender.
					OpponentBridge.ReceiveHit(mults.V, mults.C, blocked: false);
					OwnerBridge.NotifyShatterLanded();
				}
				else
				{
					OpponentBridge.NotifyParrySuccess();
				}
				return;
			}

			if (attackerAttemptingShatter)
			{
				// Shatter whiff (Framework Section 4): defender used Standard Block, not Parry.
				// The block still protects the defender normally, but the attacker is penalized:
				// Momentum is drained and a -4 frame Recovery disadvantage is applied.
				OpponentBridge.ReceiveHit(mults.V, mults.C, blocked: true);
				OwnerBridge.NotifyShatterWhiff();
				return;
			}

			if (isDeathblow)
			{
				// Deathblow: defender is already in Deathblow state, so this hit finishes them off.
				// Apply the hit as normal to trigger the defender's death sequence, but also notify
				// the attacker that they landed a Deathblow for UI purposes.
				OpponentBridge.ReceiveHit(mults.V, mults.C, blocked: false);
				OwnerBridge.NotifyDeathblowTriggered();
				return;
			}

			// Apply damage to opponent.
			OpponentBridge.ReceiveHit(mults.V, mults.C, isBlocked);

			// Notify attacker that hit landed.
			OwnerBridge.NotifyHitLanded(mults.V, mults.C, isBlocked);
		}
	}
}

