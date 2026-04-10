// TestSceneCoordinator.cs (attach to TestScene root node)
using Godot;
using ResonanceOfSteel.Bridge;


namespace ResonanceOfSteel.Scene
{
	public partial class GameCoordinator : Node3D
	{
		[Export] public PlayerBridge Player1;
		[Export] public PlayerBridge Player2;
		private HitboxManager _hitboxP1 => Player1.GetNode<HitboxManager>("HitboxManager");
		private HitboxManager _hitboxP2 => Player2.GetNode<HitboxManager>("HitboxManager");

		public override void _Ready()
		{
			// Suppress the console spam and allow P2 testing
			RegisterPlayer2Inputs();

			// Wire opponents
			Player1.Opponent = Player2;
			Player2.Opponent = Player1;

			// Wire hitbox managers
			_hitboxP1.OwnerBridge = Player1;
			_hitboxP1.OpponentBridge = Player2;

			// Give the players control over their respective hitbox managers
			Player1.ActiveHitboxManager = _hitboxP1;
			Player2.ActiveHitboxManager = _hitboxP2;

			_hitboxP2.OwnerBridge = Player2;
			_hitboxP2.OpponentBridge = Player1;
			GD.Print(_hitboxP1 + " owner: " + _hitboxP1.OwnerBridge + ", opponent: " + _hitboxP1.OpponentBridge);
			GD.Print(_hitboxP2 + " owner: " + _hitboxP2.OwnerBridge + ", opponent: " + _hitboxP2.OpponentBridge);

			// Set player indices
			Player1.PlayerIndex = 0;
			Player2.PlayerIndex = 1;

			// Position players facing each other
			Player1.GlobalPosition = new Vector3(-3, 1, 0);
			Player2.GlobalPosition = new Vector3(3, 1, 0);
		}

		private void RegisterPlayer2Inputs()
		{
			// Define the P2 actions and some default testing keys
			var p2Actions = new Godot.Collections.Dictionary<string, Key>
		{
			{ "attack_p2", Key.KpEnter },
			{ "block_parry_p2", Key.KpSubtract },
			{ "dodge_p2", Key.Backspace },
			{ "jump_p2", Key.Kp0 },
			{ "run_p2", Key.KpMultiply },
			{ "modifier_light_p2", Key.Kp1 },
			{ "modifier_heavy_p2", Key.Kp2 },
			{ "modifier_super_p2", Key.Kp3 },
			{ "move_left_p2", Key.Kp4 },
			{ "move_down_p2", Key.Kp5 },
			{ "move_right_p2", Key.Kp6 },
			{ "move_up_p2", Key.Kp8 }
		};

			foreach (var kvp in p2Actions)
			{
				if (!InputMap.HasAction(kvp.Key))
				{
					InputMap.AddAction(kvp.Key);

					// Map the physical key so we can trigger the inputs
					var inputEvent = new InputEventKey();
					inputEvent.Keycode = kvp.Value;
					InputMap.ActionAddEvent(kvp.Key, inputEvent);
				}
			}
		}
	}
}