
// Updates HUD elements each frame from PlayerBridge accessor values.
// In the Presentation Layer: reads state, never writes it.
using Godot;
using ResonanceOfSteel.Bridge;

namespace ResonanceOfSteel.Presentation
{
	public partial class HUD : CanvasLayer
	{
		[Export] public PlayerBridge Player1;
		[Export] public PlayerBridge Player2;
		[Export] public RoundManager RoundMgr;

		// P1 bars
		[Export] public ProgressBar P1VitalityBar;
		[Export] public ProgressBar P1ComposureBar;
		[Export] public ProgressBar P1MomentumBar;
		[Export] public Label P1StacksLabel;

		// P2 bars
		[Export] public ProgressBar P2VitalityBar;
		[Export] public ProgressBar P2ComposureBar;
		[Export] public ProgressBar P2MomentumBar;
		[Export] public Label P2StacksLabel;

		// Center
		[Export] public Label P1LivesLabel;
		[Export] public Label P2LivesLabel;
		[Export] public Label TimerLabel;

		// Brief Section 11.2: Momentum UI visibility toggle
		[Export] public bool ShowMomentumToOpponent = false;

		public override void _Ready()
		{
			if (RoundMgr != null)
			{
				RoundMgr.Connect(RoundManager.SignalName.LivesChanged,
					new Callable(this, nameof(OnLivesChanged)));
				RoundMgr.Connect(RoundManager.SignalName.TimerChanged,
					new Callable(this, nameof(OnTimerChanged)));

				P1LivesLabel.Text = $"P1: {RoundMgr.P1Lives}";
				P2LivesLabel.Text = $"P2: {RoundMgr.P2Lives}";
			}
		}

		public override void _Process(double delta)
		{
			if (Player1 == null || Player2 == null) return;

			// Update bars every frame.
			P1VitalityBar.Value = Player1.GetVitality();
			P1ComposureBar.Value = Player1.GetComposure();
			P1MomentumBar.Value = Player1.GetMomentum();
			P1StacksLabel.Text = $"Stacks: {Player1.GetFrameAdvantageStacks()}";

			P2VitalityBar.Value = Player2.GetVitality();
			P2ComposureBar.Value = Player2.GetComposure();
			P2MomentumBar.Value = Player2.GetMomentum();
			P2StacksLabel.Text = $"Stacks: {Player2.GetFrameAdvantageStacks()}";

			// Momentum visibility: each player sees their own, not opponent's.
			// In split-screen, both bars are visible to both players in this simple setup.
			// Full viewport-specific visibility requires per-viewport CanvasLayer (future).
		}

		private void OnLivesChanged(int p1Lives, int p2Lives)
		{
			P1LivesLabel.Text = $"P1: {p1Lives}";
			P2LivesLabel.Text = $"P2: {p2Lives}";
		}

		private void OnTimerChanged(float seconds)
		{
			int mins = (int)(seconds / 60);
			int secs = (int)(seconds % 60);
			TimerLabel.Text = $"{mins}:{secs:D2}";
		}
	}
}

