using Godot;

namespace ResonanceOfSteel.Bridge
{
	public partial class MainCoordinator : Node
	{
		public override void _Ready()
		{
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

			GD.Print("Split-screen cameras wired.");
		}
	}
}