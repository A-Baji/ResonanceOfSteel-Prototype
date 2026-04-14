// Debug overlay displaying simulation state, frame data, and economy values.
// Presentation Layer: reads state only, never writes.
// Toggle visibility at runtime with F3.
using Godot;
using ResonanceOfSteel.Bridge;

namespace ResonanceOfSteel.Presentation
{
	public sealed partial class DebugHUD : CanvasLayer
	{
		[Export] public PlayerBridge Player1;
		[Export] public PlayerBridge Player2;

		[Export] public RichTextLabel P1DebugLabel;
		[Export] public RichTextLabel P2DebugLabel;
		[Export] public Label FpsLabel;

		public override void _Ready()
		{
			// Start hidden — press F3 to toggle.
			Visible = false;
		}

		public override void _Input(InputEvent @event)
		{
			if (@event is InputEventKey key && key.Pressed && !key.Echo
				&& key.PhysicalKeycode == Key.F3)
			{
				Visible = !Visible;
			}
		}

		public override void _Process(double delta)
		{
			if (!Visible) return;

			FpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";

			if (Player1 != null)
				P1DebugLabel.Text = BuildDebugText(Player1, "P1");
			if (Player2 != null)
				P2DebugLabel.Text = BuildDebugText(Player2, "P2");
		}

		private static string BuildDebugText(PlayerBridge player, string label)
		{
			var state = player.GetDebugStateName();
			var archetype = player.GetArchetypeName();
			var vitality = player.GetVitality();
			var composure = player.GetComposure();
			var momentum = player.GetMomentum();
			var stacks = player.GetFrameAdvantageStacks();
			var blockPenalties = player.GetPrematureBlockPenalties();
			var effectiveWindow = player.GetEffectiveParryWindow();
			var tier = player.GetCurrentTier();
			var lastEvent = player.GetLastEventName();
			var bufferCount = player.GetDebugBufferCount();
			var fatigued = player.GetIsFatigued();
			var terminal = player.GetIsTerminal();
			var deathblowVuln = player.GetIsDeathblowVulnerable();
			var actionLocked = player.GetIsActionLocked();
			var hitboxActive = player.IsHitboxActive();
			var armorActive = player.GetIsArmorActive();

			return $"[b]{label} — {archetype}[/b]\n"
				+ $"[b]State:[/b] {state}\n"
				+ $"[b]Tier:[/b] {tier}\n"
				+ $"\n"
				+ $"[b]Vitality:[/b]  {vitality:F3}\n"
				+ $"[b]Composure:[/b] {composure:F3}\n"
				+ $"[b]Momentum:[/b]  {momentum:F2} / 8.0\n"
				+ $"[b]Stacks:[/b]    {stacks}\n"
				+ $"[b]Block Pen:[/b] {blockPenalties} (Window: {effectiveWindow}f)\n"
				+ $"\n"
				+ $"[b]Flags:[/b]\n"
				+ $"  ActionLocked: {BoolTag(actionLocked)}\n"
				+ $"  Hitbox: {BoolTag(hitboxActive)}  Armor: {BoolTag(armorActive)}\n"
				+ $"  Fatigued: {BoolTag(fatigued)}  Terminal: {BoolTag(terminal)}\n"
				+ $"  DB Vulnerable: {BoolTag(deathblowVuln)}\n"
				+ $"\n"
				+ $"[b]Buffer:[/b] {bufferCount} queued\n"
				+ $"[b]Last Event:[/b] {lastEvent}";
		}

		private static string BoolTag(bool value)
			=> value ? "[color=red]YES[/color]" : "[color=gray]no[/color]";
	}
}
