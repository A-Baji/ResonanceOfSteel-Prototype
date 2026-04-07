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
		ParryFailed,       // Tried to parry but timing was wrong
		ShatterEvent,      // A Shatter landed (defender parry broken)
		ClashEvent,        // Two attacks of the same tier met simultaneously
		DodgeSuccess,      // A dodge was executed
		FatigueEntered,    // Momentum hit zero
		FatigueExited,     // Momentum recovered from zero
		DeathblowTriggered,// Composure full + hit landed = execution
	}
}

