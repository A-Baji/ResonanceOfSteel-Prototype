using Godot;
using PhantomCamera;

namespace ResonanceOfSteel.Bridge
{
	public partial class MainCoordinator : Node
	{
		public override void _Ready()
		{
			var vp1 = GetNode<SubViewport>("HBoxContainer/SubViewportContainer/ViewportP1");
			var vp2 = GetNode<SubViewport>("HBoxContainer/SubViewportContainer2/ViewportP2");
			var gameWorld = GetNode<Node3D>("GameScene");

			var player1 = gameWorld.GetNode<Node3D>("Player1");
			var player2 = gameWorld.GetNode<Node3D>("Player2");

			vp2.World3D = vp1.World3D;

			var pcam1 = vp1.GetNode<Node3D>("PCam_P1");
			var pcam2 = vp2.GetNode<Node3D>("PCam_P2");

			// Apply settings after a tiny delay to ensure the GDScript 
			// internal _ready() has finished creating the dictionaries.
			Callable.From(() => ConfigureCameras(pcam1, pcam2, player1, player2)).CallDeferred();
		}

		private void ConfigureCameras(Node3D pcam1, Node3D pcam2, Node3D p1, Node3D p2)
		{
			// --- PLAYER 1 SETUP ---
			pcam1.Set("priority", 30);
			pcam1.Set("follow_mode", (int)FollowMode3D.ThirdPerson);
			pcam1.Set("look_at_mode", (int)LookAtMode.Simple);

			// Force the targets
			pcam1.Set("follow_target", p1);
			pcam1.Set("look_at_target", p2);

			// Direct Dictionary Access
			pcam1.Set("follow_parameters/distance", 5.0f);
			pcam1.Set("follow_parameters/height", 2.0f);
			pcam1.Set("follow_parameters/horizontal_offset", 1.0f);

			// --- PLAYER 2 SETUP ---
			pcam2.Set("priority", 30);
			pcam2.Set("follow_mode", (int)FollowMode3D.ThirdPerson);
			pcam2.Set("look_at_mode", (int)LookAtMode.Simple);

			// Force the targets - SWAPPED
			pcam2.Set("follow_target", p2);
			pcam2.Set("look_at_target", p1);

			// Direct Dictionary Access - MIRRORED
			pcam2.Set("follow_parameters/distance", 5.0f);
			pcam2.Set("follow_parameters/height", 2.0f);
			pcam2.Set("follow_parameters/horizontal_offset", -1.0f);

			GD.Print("Cameras Manually Reconfigured");
		}
	}
}