using Godot;
using ResonanceOfSteel.Bridge;

namespace ResonanceOfSteel.Scene
{
	public sealed partial class MainCoordinator : Node
	{
		// Victory overlay — created in code to avoid a separate scene for a single label.
		private CanvasLayer _victoryOverlay;
		private Label _victoryLabel;

		public override void _Ready()
		{
			// Allow this node to receive input even when the tree is paused (victory screen).
			ProcessMode = ProcessModeEnum.Always;

			var vp1 = GetNode<SubViewport>("HBoxContainer/SubViewportContainer/ViewportP1");
			var vp2 = GetNode<SubViewportContainer>("HBoxContainer/SubViewportContainer2")
							.GetNode<SubViewport>("ViewportP2");

			var camP1 = vp1.GetNode<Camera3D>("CameraP1");
			var camP2 = vp2.GetNode<Camera3D>("CameraP2");

			var game = GetNode<Node3D>("GameScene");
			var player1 = game.GetNode<Node3D>("Player1");
			var player2 = game.GetNode<Node3D>("Player2");

			// Wire RemoteTransform3D → Camera nodes (cross-tree path is valid).
			var rt1 = player1.GetNode<RemoteTransform3D>("CameraPivot/SpringArm/RemoteTransform");
			var rt2 = player2.GetNode<RemoteTransform3D>("CameraPivot/SpringArm/RemoteTransform");
			rt1.RemotePath = camP1.GetPath();
			rt2.RemotePath = camP2.GetPath();

			// Wire CameraController targets.
			var ctrl1 = player1.GetNode<CameraController>("CameraPivot");
			var ctrl2 = player2.GetNode<CameraController>("CameraPivot");
			ctrl1.OwnerCharacter = player1;
			ctrl1.OpponentCharacter = player2;
			ctrl2.OwnerCharacter = player2;
			ctrl2.OpponentCharacter = player1;

			// Listen for match end to show victory overlay.
			var roundMgr = game.GetNode<RoundManager>("RoundManager");
			roundMgr.MatchEnded += OnMatchEnded;

			CreateVictoryOverlay();

			GD.Print("Split-screen cameras wired.");
		}

		private void CreateVictoryOverlay()
		{
			_victoryOverlay = new CanvasLayer { Layer = 100 };
			_victoryLabel = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
				GrowHorizontal = Control.GrowDirection.Both,
				GrowVertical = Control.GrowDirection.Both
			};
			_victoryLabel.AddThemeFontSizeOverride("font_size", 64);
			_victoryOverlay.AddChild(_victoryLabel);
			_victoryOverlay.Visible = false;
			AddChild(_victoryOverlay);
		}

		private void OnMatchEnded(int winnerPlayerIndex)
		{
			int displayNum = winnerPlayerIndex + 1; // 0-indexed → 1-indexed
			_victoryLabel.Text = $"Player {displayNum} Wins!\n\nPress ESC to quit";
			_victoryOverlay.Visible = true;
			GetTree().Paused = true;
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (_victoryOverlay.Visible && @event.IsActionPressed("ui_cancel"))
				GetTree().Quit();
		}
	}
}