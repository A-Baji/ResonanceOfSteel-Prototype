using Godot;

namespace ResonanceOfSteel.Bridge
{
	public sealed partial class StageController : Node3D
	{
		public enum StageSize { Small, Medium, Large }

		[Export] public StageSize SelectedSize = StageSize.Medium;
		[Export] public MeshInstance3D Floor;
		[Export] public CollisionShape3D FloorCollision;
		[Export] public StaticBody3D NorthWall, SouthWall, EastWall, WestWall;

		public override void _Ready()
		{
			ConfigureStage();
		}

		private void ConfigureStage()
		{
			float dim = SelectedSize switch
			{
				StageSize.Small => 10f,
				StageSize.Large => 35f,
				_ => 20f // Medium Default
			};

			float half = dim / 2f;

			// 1. Resize Visual Floor
			if (Floor.Mesh is PlaneMesh pm) pm.Size = new Vector2(dim, dim);

			// 2. Resize Physics Floor
			if (FloorCollision.Shape is BoxShape3D floorBox)
			{
				// We set the box to the stage dimensions (dim x dim)
				// We give it a thickness of 1.0 so players don't tunnel through it
				floorBox.Size = new Vector3(dim, 1.0f, dim);
			}
			FloorCollision.Position = new Vector3(0, -0.5f, 0);

			// 3. Position Walls (Y=1 to act as a 2m high invisible barrier)
			UpdateWall(NorthWall, new Vector3(0, 1, -half), new Vector3(dim, 2, 0.5f));
			UpdateWall(SouthWall, new Vector3(0, 1, half), new Vector3(dim, 2, 0.5f));
			UpdateWall(EastWall, new Vector3(half, 1, 0), new Vector3(0.5f, 2, dim));
			UpdateWall(WestWall, new Vector3(-half, 1, 0), new Vector3(0.5f, 2, dim));
		}

		private void UpdateWall(StaticBody3D wall, Vector3 pos, Vector3 size)
		{
			if (wall == null) return;
			wall.Position = pos;
			var shape = wall.GetNode<CollisionShape3D>("CollisionShape3D");
			if (shape.Shape is BoxShape3D box) box.Size = size;
		}
	}
}