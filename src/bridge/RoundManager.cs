
// Manages match state: lives, timer, round resets, win conditions.
// Lives in the Bridge layer (Godot-aware) because it manages scene state.
using Godot;
using ResonanceOfSteel.Bridge;

public partial class RoundManager : Node
{
	[Export] public PlayerBridge Player1;
	[Export] public PlayerBridge Player2;
	[Export] public int StartingLives = 4;     // Brief Section 10.2
	[Export] public float RoundTimerSeconds = 210f;  // Brief Section 10.2

	// Spawn positions for round reset.
	[Export] public Vector3 Player1SpawnPos = new(-3, 1, 0);
	[Export] public Vector3 Player2SpawnPos = new(3, 1, 0);

	private int _p1Lives;
	private int _p2Lives;
	private float _timeRemaining;
	private bool _roundActive;

	// Signals for the UI to display.
	[Signal] public delegate void LivesChangedEventHandler(int p1Lives, int p2Lives);
	[Signal] public delegate void TimerChangedEventHandler(float seconds);
	[Signal] public delegate void MatchEndedEventHandler(int winnerPlayerIndex);

	public override void _Ready()
	{
		_p1Lives = StartingLives;
		_p2Lives = StartingLives;

		// Listen for Deathblow signals.
		Player1.Connect(PlayerBridge.SignalName.DeathblowTriggered,
			new Callable(this, nameof(OnPlayer1Deathblow)));
		Player2.Connect(PlayerBridge.SignalName.DeathblowTriggered,
			new Callable(this, nameof(OnPlayer2Deathblow)));

		StartRound();
	}

	public override void _Process(double delta)
	{
		if (!_roundActive) return;

		_timeRemaining -= (float)delta;
		EmitSignal(SignalName.TimerChanged, Mathf.Max(_timeRemaining, 0f));

		if (_timeRemaining <= 0f)
			ResolveTimeout();
	}

	private void StartRound()
	{
		_timeRemaining = RoundTimerSeconds;
		_roundActive = true;

		Player1.FullReset(Player1SpawnPos);
		Player2.FullReset(Player2SpawnPos);
	}

	// Called when Player 1 receives a Deathblow (Player 2 landed it).
	private void OnPlayer1Deathblow()
	{
		_roundActive = false;
		_p1Lives--;
		EmitSignal(SignalName.LivesChanged, _p1Lives, _p2Lives);
		CheckMatchEnd();
	}

	private void OnPlayer2Deathblow()
	{
		_roundActive = false;
		_p2Lives--;
		EmitSignal(SignalName.LivesChanged, _p1Lives, _p2Lives);
		CheckMatchEnd();
	}

	private void ResolveTimeout()
	{
		_roundActive = false;
		// Vitality tiebreak: higher Vitality wins the round.
		float p1V = Player1.GetVitality();
		float p2V = Player2.GetVitality();

		if (p1V > p2V) { _p2Lives--; }
		else if (p2V > p1V) { _p1Lives--; }
		// Exact tie: no life lost, start a new round.

		EmitSignal(SignalName.LivesChanged, _p1Lives, _p2Lives);
		CheckMatchEnd();
	}

	private void CheckMatchEnd()
	{
		if (_p1Lives <= 0) { EmitSignal(SignalName.MatchEnded, 1); return; }
		if (_p2Lives <= 0) { EmitSignal(SignalName.MatchEnded, 0); return; }

		// Match continues — reset after a short delay.
		var timer = GetTree().CreateTimer(2.0);
		timer.Connect("timeout", new Callable(this, nameof(StartRound)));
	}
}

