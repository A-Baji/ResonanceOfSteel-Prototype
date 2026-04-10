
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

		private Tween _p1VitTween, _p1CompTween, _p1MomTween;
		private Tween _p2VitTween, _p2CompTween, _p2MomTween;

		private System.Collections.Generic.Dictionary<ProgressBar, float> _barTargets = new();

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

			// Use AnimateBar instead of direct assignment to get the smooth tweening effect.
			// The helper already checks if the value changed before starting a new tween.
			AnimateBar(ref _p1VitTween, P1VitalityBar, Player1.GetVitality());
			AnimateBar(ref _p1CompTween, P1ComposureBar, Player1.GetComposure());
			AnimateBar(ref _p1MomTween, P1MomentumBar, Player1.GetMomentum());
			P1StacksLabel.Text = $"Stacks: {Player1.GetFrameAdvantageStacks()}";

			AnimateBar(ref _p2VitTween, P2VitalityBar, Player2.GetVitality());
			AnimateBar(ref _p2CompTween, P2ComposureBar, Player2.GetComposure());
			AnimateBar(ref _p2MomTween, P2MomentumBar, Player2.GetMomentum());
			P2StacksLabel.Text = $"Stacks: {Player2.GetFrameAdvantageStacks()}";
		}

		private void AnimateBar(ref Tween tween, ProgressBar bar, float newValue)
		{
			// Initialize target if not present
			if (!_barTargets.ContainsKey(bar)) _barTargets[bar] = (float)bar.Value;

			// ONLY start a new tween if the PLAYER'S actual value changed, 
			// not if the bar's visual value is different.
			if (Mathf.IsEqualApprox(_barTargets[bar], newValue)) return;

			// Update the record of what we are aiming for
			_barTargets[bar] = newValue;

			if (tween != null && tween.IsValid()) tween.Kill();

			tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(bar, "value", newValue, 0.25f);
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

