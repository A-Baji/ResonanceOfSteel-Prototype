using FixedMathSharp;

namespace ResonanceOfSteel.Simulation
{
	public class EconomyHandler
	{
		private readonly EconomyConstants _c;

		// The three primary resources. Normalised 0-1 except Momentum (0-8).
		public Fixed64 Momentum { get; private set; }
		public Fixed64 Composure { get; private set; }  // 0 = no composure strain; 1 = deathblow vulnerable
		public Fixed64 Vitality { get; private set; }  // 0 = dead; 1 = full health

		// Frame Advantage Stacks from Prototype Brief Section 6.3.
		public int FrameAdvantageStacks { get; private set; }

		// Derived states
		public bool IsFatigued => Momentum <= Fixed64.Zero;
		public bool IsTerminal => Vitality < _c.TerminalVitalityThreshold;
		public bool IsDeathblowVulnerable => Composure >= Fixed64.One;

		public EconomyHandler(EconomyConstants constants)
		{
			_c = constants;
			Momentum = Fixed64.Zero;      // Start with no Momentum
			Composure = Fixed64.Zero;      // Start with no Composure strain
			Vitality = Fixed64.One;       // Start at full health
		}

		// ── Momentum ──────────────────────────────────────────────────

		// Called each frame when the character is moving toward the opponent.
		// isRunning determines whether to use walk or run rate (Brief Section 6.1).
		public void AddRightOfWayMomentum(bool isRunning)
		{
			var rate = isRunning ? _c.RunMomentumRate : _c.WalkMomentumRate;
			Momentum = FixedMath.Min(Momentum + rate, _c.MomentumMax);
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

		// Called each physics frame. Implements R_comp = B_rate * (V_curr / V_max).
		// Brief Section 6.2: recovery stops when Terminal.
		public void TickComposureRecovery()
		{
			if (IsTerminal) return;
			var recovery = _c.ComposureBaseRecoveryRate * Vitality; // V_max is always 1
			Composure = FixedMath.Max(Composure - recovery, Fixed64.Zero);
		}

		// Called when this character is hit or blocked.
		// multiplier comes from DamageMultipliers in FrameData.cs.
		public void ApplyComposureDamage(Fixed64 multiplier)
		{
			Composure = FixedMath.Min(Composure + _c.BaseComposureDamage * multiplier, Fixed64.One);
		}

		// ── Vitality ──────────────────────────────────────────────────

		public void ApplyVitalityDamage(Fixed64 multiplier)
		{
			Vitality = FixedMath.Max(Vitality - _c.BaseVitalityDamage * multiplier, Fixed64.Zero);
		}

		// ── Frame Advantage Stacks ─────────────────────────────────────

		// Called on each successful Perfect Parry (Brief Section 6.3).
		public void IncrementFrameAdvantage()
		{
			FrameAdvantageStacks++;
		}

		// Called on: Lethality damage, failed parry, attack initiated, Standard Block input.
		public void ResetFrameAdvantage()
		{
			FrameAdvantageStacks = 0;
		}

		// Returns the Coil reduction in frames if threshold is met, otherwise 0.
		public int GetFrameAdvantageCoilReduction()
		{
			return FrameAdvantageStacks >= _c.FrameAdvantageThreshold
				? _c.FrameAdvantageOffset
				: 0;
		}

		// ── Full reset (called between rounds) ─────────────────────────
		public void FullReset()
		{
			Momentum = Fixed64.Zero;
			Composure = Fixed64.Zero;
			Vitality = Fixed64.One;
			FrameAdvantageStacks = 0;
		}
	}
}

