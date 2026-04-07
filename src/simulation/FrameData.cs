// All values are frame counts at 60fps, from Prototype Brief Section 8.
namespace ResonanceOfSteel.Simulation
{
	public static class LongswordFrames
	{
		// Flick (Tier 0)
		public const int FlickCoil = 4;
		public const int FlickSwing = 2;
		public const int FlickRecovery = 4;

		// Cross Cut (Tier 1)
		public const int CrossCutCoil = 8;
		public const int CrossCutSwing = 4;
		public const int CrossCutRecovery = 6;

		// Overhead (Tier 2)
		public const int OverheadCoil = 16;
		public const int OverheadSwing = 6;
		public const int OverheadRecovery = 10;

		// Lunge (Tier 3)
		public const int LungeCoil = 24;
		public const int LungeSwing = 8;
		public const int LungeRecovery = 13;
	}

	public static class GreatswordFrames
	{
		// Pommel Strike (Tier 0)
		public const int PommelCoil = 8;
		public const int PommelSwing = 3;
		public const int PommelRecovery = 6;

		// Wide Slash (Tier 1)
		public const int WideSlashCoil = 14;
		public const int WideSlashSwing = 6;
		public const int WideSlashRecovery = 10;

		// Crush (Tier 2)
		public const int CrushCoil = 24;
		public const int CrushSwing = 8;
		public const int CrushRecovery = 16;

		// Cleave (Tier 3)
		public const int CleaveCoil = 36;
		public const int CleaveSwing = 10;
		public const int CleaveRecovery = 20;
	}

	public static class DamageMultipliers
	{
		// Longsword - (VitalityMult, ComposureMult)
		// Composure multiplier for Flick and Pommel only applies when blocked.
		public static readonly (float V, float C) Flick = (0.2f, 0.1f);
		public static readonly (float V, float C) CrossCut = (1.0f, 1.0f);
		public static readonly (float V, float C) Overhead = (1.5f, 1.75f);
		public static readonly (float V, float C) Lunge = (2.0f, 2.0f);

		// Greatsword
		public static readonly (float V, float C) PommelStrike = (0.3f, 0.15f);
		public static readonly (float V, float C) WideSlash = (1.4f, 1.2f);
		public static readonly (float V, float C) Crush = (2.0f, 2.25f);
		public static readonly (float V, float C) Cleave = (2.75f, 2.75f);
	}
}

