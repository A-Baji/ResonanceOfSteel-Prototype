using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	public sealed class EconomyHandler
	{
		private readonly EconomyConstants _c;

		// The three primary resources. Normalised 0-1 except Momentum (0-8).
		public Fixed64 Momentum { get; private set; }
		public Fixed64 Composure { get; private set; }  // 0 = no composure strain; 1 = deathblow vulnerable
		public Fixed64 Vitality { get; private set; }  // 0 = dead; 1 = full health

		// Frame Advantage Stacks from Prototype Brief Section 6.3.
		public int FrameAdvantageStacks { get; private set; }

		// Composure recovery cooldown — frames remaining before recovery can tick.
		private int _composureCooldownRemaining;

		// Derived states
		public bool IsFatigued => Momentum <= Fixed64.Zero;
		public bool IsTerminal => Vitality < _c.TerminalVitalityThreshold;
		public bool IsDeathblowVulnerable => Composure >= Fixed64.One || Vitality <= Fixed64.Zero;

		public EconomyHandler(EconomyConstants constants)
		{
			_c = constants;
			Momentum = _c.MomentumMax / (Fixed64)2.0;
			Composure = Fixed64.Zero;
			Vitality = Fixed64.One;
		}

		// ── Momentum ──────────────────────────────────────────────────

		// Called each frame with the signed displacement component toward the opponent.
		// Positive = moving toward (gain momentum). Negative should use DrainRetreatMomentum.
		public void AddRightOfWayMomentum(Fixed64 displacementToward)
		{
			var gain = _c.RoWMomentumPerUnit * displacementToward;
			Momentum = FixedMath.Min(Momentum + gain, _c.MomentumMax);
		}

		// Called each frame when moving away from opponent. Drains momentum.
		public void DrainRetreatMomentum(Fixed64 displacementAway)
		{
			var drain = _c.RetreatDrainPerUnit * displacementAway;
			Momentum = FixedMath.Max(Momentum - drain, Fixed64.Zero);
		}

		// Called when any strike lands or is blocked.
		public void AddMomentumOnHit()
		{
			Momentum = FixedMath.Min(Momentum + _c.BaseMomentumOnHit, _c.MomentumMax);
		}

		// Called on a Clash event.
		public void AddClashSurge()
		{
			Momentum = FixedMath.Min(Momentum + _c.ClashMomentumSurge, _c.MomentumMax);
		}

		// Returns false if insufficient Momentum. Does NOT deduct — caller decides.
		public bool CanAfford(Fixed64 cost) => Momentum >= cost;

		public void SpendMomentum(Fixed64 cost)
		{
			Momentum = FixedMath.Max(Momentum - cost, Fixed64.Zero);
		}

		// ── Composure ─────────────────────────────────────────────────

		// Called each physics frame. Sekiro-inspired recovery:
		// 1. Recovery is paused for ComposureRecoveryCooldownFrames after taking composure damage.
		// 2. Recovery rate scales with Vitality² (quadratic), not linear.
		//    At 50% HP → 25% recovery rate. At 25% HP → 6.25%. Makes vitality damage meaningful.
		// 3. Terminal vitality stops recovery entirely.
		public void TickComposureRecovery()
		{
			if (IsTerminal) return;
			if (_composureCooldownRemaining > 0)
			{
				_composureCooldownRemaining--;
				return;
			}
			var vSquared = Vitality * Vitality; // Quadratic scaling
			var recovery = _c.ComposureBaseRecoveryRate * vSquared;
			Composure = FixedMath.Max(Composure - recovery, Fixed64.Zero);
		}

		// Called when this character is hit or blocked.
		// multiplier comes from MoveData returned by IArchetypeData.GetMoveData().
		public void ApplyComposureDamage(Fixed64 multiplier)
		{
			Composure = FixedMath.Min(Composure + _c.BaseComposureDamage * multiplier, Fixed64.One);
			_composureCooldownRemaining = _c.ComposureRecoveryCooldownFrames;
		}

		// ── Vitality ──────────────────────────────────────────────────

		public void ApplyVitalityDamage(Fixed64 multiplier)
		{
			Vitality = FixedMath.Max(Vitality - _c.BaseVitalityDamage * multiplier, Fixed64.Zero);
		}

		// ── Frame Advantage Stacks ─────────────────────────────────────

		// Frames since the last successful parry — drives gradual stack decay.
		private int _framesSinceLastParry;
		private int _decayAccumulator;

		// Called on each successful Perfect Parry.
		public void IncrementFrameAdvantage()
		{
			FrameAdvantageStacks++;
			_framesSinceLastParry = 0;
			_decayAccumulator = 0;
		}

		// Called on: non-parried hit received (blocked or unblocked).
		public void ResetFrameAdvantage()
		{
			FrameAdvantageStacks = 0;
			_framesSinceLastParry = 0;
			_decayAccumulator = 0;
		}

		// Consumes all stacks and returns total Coil reduction (stacks × offset).
		public int ConsumeFrameAdvantageCoilReduction()
		{
			if (FrameAdvantageStacks <= 0) return 0;
			int reduction = FrameAdvantageStacks * _c.FrameAdvantageOffset;
			FrameAdvantageStacks = 0;
			_framesSinceLastParry = 0;
			_decayAccumulator = 0;
			return reduction;
		}

		// Called each tick. After StackDecayDelayFrames of no parrying,
		// stacks decay by 1 every StackDecayIntervalFrames.
		public void TickStackDecay()
		{
			if (FrameAdvantageStacks <= 0) return;
			_framesSinceLastParry++;
			if (_framesSinceLastParry < _c.StackDecayDelayFrames) return;

			_decayAccumulator++;
			if (_decayAccumulator >= _c.StackDecayIntervalFrames)
			{
				FrameAdvantageStacks--;
				_decayAccumulator = 0;
			}
		}

		// ── Full reset (called between rounds) ─────────────────────────
		public void FullReset()
		{
			Momentum = _c.MomentumMax / (Fixed64)2.0;
			Composure = Fixed64.Zero;
			Vitality = Fixed64.One;
			FrameAdvantageStacks = 0;
			_composureCooldownRemaining = 0;
			_framesSinceLastParry = 0;
			_decayAccumulator = 0;
		}
	}
}

