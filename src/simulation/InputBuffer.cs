
// A Time-To-Live queue for buffering player inputs.
// Implements Prototype Brief Section 4.2.
using System.Collections.Generic;

namespace ResonanceOfSteel.Simulation
{
	public sealed class InputBuffer
	{
		private readonly int _ttl;

		private static readonly PlayerInputAction[] Priority =
		{
			PlayerInputAction.BlockParry,
			PlayerInputAction.Attack,
			PlayerInputAction.Dodge,
			PlayerInputAction.Jump,
		};

		// Each entry: (action, frames remaining)
		private readonly List<(PlayerInputAction action, int ttl)> _queue = new();

		public int Count => _queue.Count;

		public InputBuffer(int ttl)
		{
			_ttl = ttl;
		}

		// Add an action to the buffer with full TTL.
		public void Add(PlayerInputAction action)
		{
			// Don't add duplicates if the same action is already buffered.
			foreach (var (a, _) in _queue)
				if (a == action) return;
			_queue.Add((action, _ttl));
		}

		// Decrement all TTLs. Remove expired entries.
		// Called once per Tick() after consumption.
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
		// Priority from Brief Section 4.2: block_parry > attack > dodge > jump
		public PlayerInputAction Consume()
		{
			foreach (var p in Priority)
				for (int i = 0; i < _queue.Count; i++)
					if (_queue[i].action == p)
					{
						_queue.RemoveAt(i);
						return p;
					}

			return PlayerInputAction.None;
		}

		public void Clear() => _queue.Clear();
	}
}

