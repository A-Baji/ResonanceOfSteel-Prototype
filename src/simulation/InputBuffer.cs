
// A Time-To-Live queue for buffering player inputs.
// Implements Prototype Brief Section 4.2.
using System.Collections.Generic;

namespace ResonanceOfSteel.Simulation
{
	public class InputBuffer
	{
		private const int DefaultTTL = 3; // 3 frames at 60fps (Brief Section 4.2)

		// Each entry: (action, frames remaining)
		private readonly List<(PlayerInputAction action, int ttl)> _queue = new();

		// Add an action to the buffer with full TTL.
		public void Add(PlayerInputAction action)
		{
			// Don't add duplicates if the same action is already buffered.
			foreach (var (a, _) in _queue)
				if (a == action) return;
			_queue.Add((action, DefaultTTL));
		}

		// Decrement all TTLs. Remove expired entries.
		// Called once per Tick() before consumption.
		public void Tick()
		{
			for (int i = _queue.Count - 1; i >= 0; i--)
			{
				var (action, ttl) = _queue[i];
				if (ttl - 1 <= 0)
					_queue.RemoveAt(i);
				else
					_queue[i] = (action, ttl - 1);
			}
		}

		// Consume the highest-priority buffered action.
		// Priority from Brief Section 4.2: block_parry > attack > dodge > jump > run
		public PlayerInputAction Consume()
		{
			var priority = new[]
			{
				PlayerInputAction.BlockParry,
				PlayerInputAction.Attack,
				PlayerInputAction.Dodge,
				PlayerInputAction.Jump,
				PlayerInputAction.Run,
			};

			foreach (var p in priority)
				for (int i = 0; i < _queue.Count; i++)
					if (_queue[i].action == p)
					{
						_queue.RemoveAt(i);
						return p;
					}

			return PlayerInputAction.None;
		}

		public bool HasAny() => _queue.Count > 0;
		public void Clear() => _queue.Clear();
	}
}

