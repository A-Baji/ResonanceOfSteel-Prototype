// State records for the LogicBlocks state machine.
// All from Prototype Brief Section 5.
namespace ResonanceOfSteel.Simulation.States
{
	// ── Grounded movement states ────────────────────────────────────────
	public record Idle;
	public record Moving(bool IsRunning);

	// ── Attack states (carry tier and frame countdown) ──────────────────
	// FramesLeft counts down each Tick(). Transition when it reaches 0.
	public record Coil(AttackTier Tier, int FramesLeft, bool HasFrameAdvantage);
	public record Swing(AttackTier Tier, int FramesLeft, bool IsShatter);
	public record Recovery(int FramesLeft, AttackTier Tier);

	// ── Defensive states ───────────────────────────────────────────────
	public record Blocking;
	public record Parrying(int FramesLeft);   // Active parry window
	public record Dodging(int FramesLeft);
	public record Jumping(int FramesLeft);

	// ── Damage states ──────────────────────────────────────────────────
	public record Staggered(int FramesLeft);
	public record Fatigued;                   // Momentum at zero

	// ── Terminal state ─────────────────────────────────────────────────
	public record Deathblow;                  // Execution animation, no inputs accepted
}

