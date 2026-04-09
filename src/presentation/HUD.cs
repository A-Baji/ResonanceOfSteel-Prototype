using Godot;
using ResonanceOfSteel.Bridge;

public partial class HUD : Godot.Label
{
	[Export] public PlayerBridge TargetPlayer;

	public override void _Process(double delta)
	{
		if (TargetPlayer != null)
		{
			var vitality = TargetPlayer.GetVitality();
			var composure = TargetPlayer.GetComposure();
			var momentum = TargetPlayer.GetMomentum();
			Text = $"State: {TargetPlayer.GetStateName()}\n" +
				   $"Vitality: {vitality:F2}\n" +
				   $"Composure: {composure:F2}\n" +
				   $"Momentum: {momentum:F2}";
		}
	}
}
