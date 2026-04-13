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
	}
}