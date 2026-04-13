namespace ResonanceOfSteel.Simulation
{
	// Every thing that can happen in combat that the presentation needs to know about.
	// The Bridge Layer polls these each tick and fires Godot signals for them.
	public enum CombatEvent
	{
		None,
		HitLand,           // An unblocked hit connected
		HitBlocked,        // A hit was blocked (Standard Block)
		ParrySuccess,      // A Perfect Parry succeeded
		ShatterEvent,      // A Shatter landed (defender parry broken)
		ShatterWhiff,      // Shatter attempted but defender was not Parrying — attacker penalized
		ClashEvent,        // Two attacks of the same tier met simultaneously
		DeathblowTriggered,// Composure full + hit landed = execution
	}
}

