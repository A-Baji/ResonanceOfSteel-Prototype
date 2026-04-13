using Godot;
using ResonanceOfSteel.Bridge;

namespace ResonanceOfSteel.Scene
{
	public sealed partial class GameCoordinator : Node3D
	{
		[Export] public PlayerBridge Player1;
		[Export] public PlayerBridge Player2;

		public override void _Ready()
		{
			RegisterPlayer2Inputs();

			var hitboxP1 = Player1.GetNode<HitboxManager>("HitboxManager");
			var hitboxP2 = Player2.GetNode<HitboxManager>("HitboxManager");

			Player1.Opponent = Player2;
			Player2.Opponent = Player1;

			hitboxP1.OwnerBridge = Player1;
			hitboxP1.OpponentBridge = Player2;
			Player1.ActiveHitboxManager = hitboxP1;

			hitboxP2.OwnerBridge = Player2;
			hitboxP2.OpponentBridge = Player1;
			Player2.ActiveHitboxManager = hitboxP2;

			// Disable individual _PhysicsProcess — coordinator drives both players.
			Player1.SetCoordinatorDriven();
			Player2.SetCoordinatorDriven();

			// PlayerIndex is set via [Export] in the scene Inspector.
			// CacheActionNames() runs lazily on first TickPhase to read the correct value.
		}

		public override void _PhysicsProcess(double delta)
		{
			// Two-pass update ensures symmetric state for clash detection.
			// Phase 1: Both players tick (state machines advance, movement applies).
			Player1.TickPhase(delta);
			Player2.TickPhase(delta);

			// Phase 2: Both players resolve hitboxes (both states are current).
			Player1.ResolvePhase();
			Player2.ResolvePhase();
		}

		private void RegisterPlayer2Inputs()
		{
			// P2 actions mapped per CLAUDE.md §9.1.
			// Movement: Arrow Keys. Actions: Numpad.
			var p2Actions = new Godot.Collections.Dictionary<string, Key>
			{
				{ "attack_p2", Key.Kp5 },
				{ "block_parry_p2", Key.KpEnter },
				{ "dodge_p2", Key.KpAdd },
				{ "jump_p2", Key.Kp0 },
				{ "run_p2", Key.KpMultiply },
				{ "modifier_light_p2", Key.Kp1 },
				{ "modifier_heavy_p2", Key.Kp2 },
				{ "modifier_super_p2", Key.Kp3 },
				{ "move_left_p2", Key.Left },
				{ "move_down_p2", Key.Down },
				{ "move_right_p2", Key.Right },
				{ "move_up_p2", Key.Up }
			};

			foreach (var kvp in p2Actions)
			{
				if (!InputMap.HasAction(kvp.Key))
				{
					InputMap.AddAction(kvp.Key);

					// Use PhysicalKeycode to match P1's project.godot definitions.
					var inputEvent = new InputEventKey();
					inputEvent.PhysicalKeycode = kvp.Value;
					InputMap.ActionAddEvent(kvp.Key, inputEvent);
				}
			}
		}
	}
}