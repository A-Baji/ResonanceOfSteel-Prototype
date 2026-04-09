using Godot;
using ResonanceOfSteel.Bridge;

// Changed class name from DebugLabel to Label to match Label.cs
public partial class Label : Godot.Label
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
