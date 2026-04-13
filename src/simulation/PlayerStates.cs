// PHASE 2: Migrate to Chickensoft LogicBlocks for serializable snapshots,
// compile-time exhaustiveness, and auto-generated state diagrams.
namespace ResonanceOfSteel.Simulation.States
{
	// ── Grounded movement states ────────────────────────────────────────
	public sealed record Idle;
	public sealed record Moving(bool IsRunning);

	// ── Attack states (carry tier and frame countdown) ──────────────────
	// FramesLeft counts down each Tick(). Transition when it reaches 0.
	public sealed record Coil(AttackTier Tier, int FramesLeft);
	public sealed record Swing(AttackTier Tier, int FramesLeft);
	public sealed record Recovery(int FramesLeft, AttackTier Tier);

	// ── Defensive states ───────────────────────────────────────────────
	public sealed record Blocking;
	public sealed record Parrying(int FramesLeft);   // Active parry window
	public sealed record Dodging(int StartupLeft, int ActiveLeft, int RecoveryLeft);
	public sealed record Jumping(int StartupLeft, int ActiveLeft, int RecoveryLeft);

	// ── Damage states ──────────────────────────────────────────────────
	public sealed record Staggered(int FramesLeft);

	// ── Terminal state ─────────────────────────────────────────────────
	public sealed record Deathblow;                  // Execution animation, no inputs accepted
}

