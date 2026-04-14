// Manages manual physics queries for hit detection.
// Runs IntersectShape() each frame during active Swing states.
//
// ARCHITECTURAL NOTE: Combat resolution lives here (Bridge) rather than in a
// pure Simulation CombatResolver because resolution requires the hit/miss boolean
// from Godot's PhysicsDirectSpaceState3D — an inherently engine-dependent query.
// All state mutations flow through PlayerSimulation's public API (OnHitReceived,
// OnClash, etc.), keeping the Simulation layer the source of truth for combat state.
using Godot;
using FixedMathSharp;
using ResonanceOfSteel.Simulation;
using ResonanceOfSteel.Simulation.Archetypes;
using ResonanceOfSteel.Bridge.Archetypes;

namespace ResonanceOfSteel.Bridge
{
	public sealed partial class HitboxManager : Node3D
	{
		// Distance from character origin to effective weapon hilt (attack origin point).
		// Consistent across all archetypes and tiers — the hitbox shape itself varies.
		private const float HiltOffset = 1.0f;

		public PlayerBridge OwnerBridge { get; set; }
		public PlayerBridge OpponentBridge { get; set; }
		public IArchetypeVisuals ArchetypeVisuals { get; set; }
		public IArchetypeData ArchetypeData { get; set; }

		// Collision layer for hurtbox detection (Brief §10.1 — Hurtbox is Layer 3, bit mask = 4)
		private const uint HurtboxLayer = 4;

		private bool _hitRegisteredThisSwing;

		// Cached per-instance physics query exclusion list (avoids per-frame allocation).
		private Godot.Collections.Array<Rid> _excludeRids;
		private Area3D _ownerHurtbox;

		// Cached query parameters to avoid per-frame heap allocation during Swing.
		private PhysicsShapeQueryParameters3D _queryParams;

		public void ProcessHitboxes()
		{
			if (OwnerBridge == null || OpponentBridge == null) return;

			if (!OwnerBridge.IsHitboxActive())
			{
				_hitRegisteredThisSwing = false;
				return;
			}

			if (_hitRegisteredThisSwing) return;

			// If a Clash was already resolved by the opponent's HitboxManager this frame,
			// skip further resolution to prevent the second pass from treating it as a Hit.
			if (OwnerBridge.IsClashedThisFrame()) return;

			if (QueryHitbox())
				ResolveHit();
		}

		private bool QueryHitbox()
		{
			var spaceState = GetWorld3D().DirectSpaceState;
			var shape = ArchetypeVisuals.GetHitboxShape(OwnerBridge.GetCurrentTier());

			if (_queryParams == null)
			{
				_ownerHurtbox = OwnerBridge.GetNode<Area3D>("Hurtbox");
				_excludeRids = new Godot.Collections.Array<Rid>
				{
					OwnerBridge.GetRid(),
					_ownerHurtbox.GetRid()
				};
				_queryParams = new PhysicsShapeQueryParameters3D
				{
					CollisionMask = HurtboxLayer,
					CollideWithAreas = true,
					CollideWithBodies = false,
					Exclude = _excludeRids
				};
			}

			Transform3D hitTransform = OwnerBridge.GlobalTransform;
			Vector3 forwardDirection = -hitTransform.Basis.Z;
			hitTransform.Origin += forwardDirection * HiltOffset;

			_queryParams.Shape = shape;
			_queryParams.Transform = hitTransform;

			return spaceState.IntersectShape(_queryParams).Count > 0;
		}

		private void ResolveHit()
		{
			_hitRegisteredThisSwing = true;

			var tier = OwnerBridge.GetCurrentTier();
			var move = ArchetypeData.GetMoveData(tier);

			// Build snapshots of both players' combat-relevant state.
			var attackerSnap = new CombatSnapshot(
				isBlocking: OwnerBridge.IsBlocking(),
				isParrying: OwnerBridge.IsParrying(),
				isInDeathblow: OwnerBridge.IsInDeathblow(),
				isArmorActive: OwnerBridge.IsArmorActive(),
				isHitboxActive: OwnerBridge.IsHitboxActive(),
				isInShatterWindow: OwnerBridge.IsInShatterWindow(),
				clashedThisFrame: OwnerBridge.IsClashedThisFrame(),
				currentTier: tier);

			var defenderSnap = new CombatSnapshot(
				isBlocking: OpponentBridge.IsBlocking(),
				isParrying: OpponentBridge.IsParrying(),
				isInDeathblow: OpponentBridge.IsInDeathblow(),
				isArmorActive: OpponentBridge.IsArmorActive(),
				isHitboxActive: OpponentBridge.IsHitboxActive(),
				isInShatterWindow: OpponentBridge.IsInShatterWindow(),
				clashedThisFrame: OpponentBridge.IsClashedThisFrame(),
				currentTier: OpponentBridge.GetCurrentTier());

			// Check shatter affordability without side effects.
			// TryInitiateShatter() deducts momentum, so we only call it
			// during ApplyCombatResult when the outcome requires it.
			bool canAffordShatter = attackerSnap.IsInShatterWindow
				&& OwnerBridge.CanAffordShatter();

			var result = CombatResolver.Resolve(attackerSnap, defenderSnap, move,
				OwnerBridge.GetArmorTradeLethality(), canAffordShatter);

			// Apply result to both simulations + bridge effects.
			ApplyCombatResult(result, move);
		}

		private void ApplyCombatResult(CombatResult result, MoveData move)
		{
			switch (result.Outcome)
			{
				case HitOutcome.Clash:
					OwnerBridge.NotifyClash();
					OpponentBridge.NotifyClash();
					if (move.KnockbackDistance > 0)
					{
						var clashDir = (OpponentBridge.GlobalPosition - OwnerBridge.GlobalPosition).Normalized();
						clashDir.Y = 0;
						OwnerBridge.ApplyKnockback(-clashDir, move.KnockbackDistance);
						OpponentBridge.ApplyKnockback(clashDir, move.KnockbackDistance);
					}
					break;

				case HitOutcome.ArmorTrade:
					OpponentBridge.ReceiveHit(result.VitalityMultiplier, result.ComposureMultiplier,
						blocked: false, result.AttackerTier, staggerFrames: 0);
					OwnerBridge.NotifyHitLanded(move.VitalityMultiplier,
						move.ComposureMultiplier, blocked: false);
					break;

				case HitOutcome.ShatterLanded:
					OwnerBridge.TryInitiateShatter(); // Pay 3.0 Momentum cost
					OpponentBridge.ReceiveHit(result.VitalityMultiplier,
						result.ComposureMultiplier, blocked: false,
						result.AttackerTier, result.StaggerFrames);
					OwnerBridge.NotifyShatterLanded();
					break;

				case HitOutcome.ParrySuccess:
					OpponentBridge.NotifyParrySuccess();
					break;

				case HitOutcome.ShatterWhiff:
					OwnerBridge.TryInitiateShatter(); // Pay 3.0 Momentum cost
					OwnerBridge.NotifyShatterWhiff();
					break;

				case HitOutcome.Deathblow:
					OpponentBridge.ReceiveHit(result.VitalityMultiplier,
						result.ComposureMultiplier, blocked: false,
						result.AttackerTier, result.StaggerFrames);
					OwnerBridge.NotifyDeathblowTriggered();
					break;

				case HitOutcome.Blocked:
					OpponentBridge.ReceiveHit(result.VitalityMultiplier,
						result.ComposureMultiplier, blocked: true,
						result.AttackerTier, result.StaggerFrames);
					OwnerBridge.NotifyHitLanded(move.VitalityMultiplier,
						move.ComposureMultiplier, blocked: true);
					if (move.KnockbackDistance > 0 && result.AttackerTier != AttackTier.Light)
					{
						var dir = (OpponentBridge.GlobalPosition - OwnerBridge.GlobalPosition).Normalized();
						dir.Y = 0;
						OpponentBridge.ApplyKnockback(dir, move.KnockbackDistance);
					}
					break;

				case HitOutcome.Hit:
					OpponentBridge.ReceiveHit(result.VitalityMultiplier,
						result.ComposureMultiplier, blocked: false,
						result.AttackerTier, result.StaggerFrames);
					OwnerBridge.NotifyHitLanded(move.VitalityMultiplier,
						move.ComposureMultiplier, blocked: false);
					if (move.KnockbackDistance > 0)
					{
						var dir = (OpponentBridge.GlobalPosition - OwnerBridge.GlobalPosition).Normalized();
						dir.Y = 0;
						OpponentBridge.ApplyKnockback(dir,
							move.KnockbackDistance * OwnerBridge.StaggerKnockbackMultiplier);
					}
					break;
			}
		}
	}
}

