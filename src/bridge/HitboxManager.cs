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

			// ── Clash detection ──────────────────────────────────────────
			// Guard: if either player already recorded a clash this frame
			// (set by the other HitboxManager), skip to avoid double-processing.
			if (OwnerBridge.IsClashedThisFrame() || OpponentBridge.IsClashedThisFrame())
				return;

			bool opponentSwinging = OpponentBridge.IsHitboxActive();
			AttackTier opponentTier = OpponentBridge.GetCurrentTier();
			if (opponentSwinging && opponentTier == tier)
			{
				OwnerBridge.NotifyClash();
				OpponentBridge.NotifyClash();

				// Clash knockback — both players pushed apart based on clashing move tier.
				if (move.KnockbackDistance > 0)
				{
					var clashDir = (OpponentBridge.GlobalPosition - OwnerBridge.GlobalPosition).Normalized();
					clashDir.Y = 0;
					OwnerBridge.ApplyKnockback(-clashDir, move.KnockbackDistance);
					OpponentBridge.ApplyKnockback(clashDir, move.KnockbackDistance);
				}
				return;
			}

			// ── Semantic state queries (no string comparison) ────────────
			bool isBlocked = OpponentBridge.IsBlocking() || OpponentBridge.IsParrying();
			bool isParried = OpponentBridge.IsParrying();
			bool isInDeathblow = OpponentBridge.IsInDeathblow();
			bool attackerInShatterWindow = OwnerBridge.IsInShatterWindow();

			// ── Armor trade: defender in Tier 3 Swing with active armor ──
			if (OpponentBridge.IsArmorActive())
			{
				var armorVMult = move.VitalityMultiplier * OwnerBridge.GetArmorTradeLethality();
				OpponentBridge.ReceiveHit(armorVMult, move.ComposureMultiplier,
					blocked: false, tier, staggerFrames: 0);
				OwnerBridge.NotifyHitLanded(move.VitalityMultiplier,
					move.ComposureMultiplier, blocked: false);
				return;
			}

			// ── Parry / Shatter resolution ───────────────────────────────
			if (isParried)
			{
				// Shatter: attacker must be in shatter window AND afford the cost.
				// TryInitiateShatter pays the 3.0 Momentum cost on success.
				bool isShatter = attackerInShatterWindow && OwnerBridge.TryInitiateShatter();
				if (isShatter)
				{
					// Shatter breaks the parry — full damage applies.
					OpponentBridge.ReceiveHit(move.VitalityMultiplier,
						move.ComposureMultiplier, blocked: false, tier, move.StaggerFrames);
					OwnerBridge.NotifyShatterLanded();
				}
				else
				{
					OpponentBridge.NotifyParrySuccess();
				}
				return;
			}

			// ── Shatter whiff (attacker in shatter window but defender is NOT parrying) ─
			// The swing visually connects but deals no damage — the Shatter commitment
			// nullifies the hit. Attacker pays 3.0 Momentum AND suffers extra recovery.
			// No momentum reward, no damage to defender.
			if (attackerInShatterWindow && OwnerBridge.TryInitiateShatter())
			{
				OwnerBridge.NotifyShatterWhiff();
				return;
			}

			// ── Deathblow execution ──────────────────────────────────────
			// Shatter whiff takes priority: once in deathblow the round is already
			// decided, so any strike (including a whiffed Shatter) initiates the
			// execution animation. Shatter whiff is checked above.
			if (isInDeathblow)
			{
				OpponentBridge.ReceiveHit(move.VitalityMultiplier,
					move.ComposureMultiplier, blocked: false, tier, move.StaggerFrames);
				OwnerBridge.NotifyDeathblowTriggered();
				return;
			}

			// ── Standard hit / block ─────────────────────────────────────
			OpponentBridge.ReceiveHit(move.VitalityMultiplier,
				move.ComposureMultiplier, isBlocked, tier, move.StaggerFrames);
			OwnerBridge.NotifyHitLanded(move.VitalityMultiplier,
				move.ComposureMultiplier, isBlocked);

			// ── Knockback on block (T1+ only) ────────────────────────────
			if (move.KnockbackDistance > 0)
			{
				var dir = (OpponentBridge.GlobalPosition - OwnerBridge.GlobalPosition).Normalized();
				dir.Y = 0;
				if (isBlocked && tier != AttackTier.Light)
				{
					OpponentBridge.ApplyKnockback(dir, move.KnockbackDistance);
				}
				else if (!isBlocked)
				{
					// Stagger knockback: 20% of block knockback (StaggerKnockbackMultiplier).
					OpponentBridge.ApplyKnockback(dir,
						move.KnockbackDistance * OwnerBridge.StaggerKnockbackMultiplier);
				}
			}
		}
	}
}

