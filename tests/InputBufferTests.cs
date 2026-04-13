using GdUnit4;
using static GdUnit4.Assertions;
using ResonanceOfSteel.Simulation;
using static ResonanceOfSteel.Tests.TestHelpers;

namespace ResonanceOfSteel.Tests
{
	[TestSuite]
	public partial class InputBufferTests
	{
		[TestCase]
		public void Add_And_Consume_Returns_Action()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Attack);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.Attack);
		}

		[TestCase]
		public void Consume_Empty_Returns_None()
		{
			var buffer = new InputBuffer(6);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.None);
		}

		[TestCase]
		public void Priority_BlockParry_Over_Attack()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Attack);
			buffer.Add(PlayerInputAction.BlockParry);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.BlockParry);
		}

		[TestCase]
		public void Priority_Attack_Over_Dodge()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Dodge);
			buffer.Add(PlayerInputAction.Attack);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.Attack);
		}

		[TestCase]
		public void Priority_Dodge_Over_Jump()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Jump);
			buffer.Add(PlayerInputAction.Dodge);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.Dodge);
		}

		[TestCase]
		public void Full_Priority_Order()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Jump);
			buffer.Add(PlayerInputAction.Dodge);
			buffer.Add(PlayerInputAction.Attack);
			buffer.Add(PlayerInputAction.BlockParry);

			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.BlockParry);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.Attack);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.Dodge);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.Jump);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.None);
		}

		[TestCase]
		public void Duplicate_Rejected()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Attack);
			buffer.Add(PlayerInputAction.Attack);
			AssertThat(buffer.Count).IsEqual(1);
		}

		[TestCase]
		public void TTL_Expiry_After_N_Ticks()
		{
			var buffer = new InputBuffer(3);
			buffer.Add(PlayerInputAction.Attack);
			buffer.Tick();  // TTL 3→2
			buffer.Tick();  // TTL 2→1
			buffer.Tick();  // TTL 1→0 (removed)
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.None);
		}

		[TestCase]
		public void TTL_Survives_Before_Expiry()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Attack);
			buffer.Tick();  // 6→5
			buffer.Tick();  // 5→4
			buffer.Tick();  // 4→3
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.Attack);
		}

		[TestCase]
		public void Default_TTL_Is_Six_Frames()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Attack);
			for (int i = 0; i < 5; i++) buffer.Tick();
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.Attack);
			// Sixth tick would expire it
			buffer.Add(PlayerInputAction.Attack);
			for (int i = 0; i < 6; i++) buffer.Tick();
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.None);
		}

		[TestCase]
		public void Clear_Empties_Buffer()
		{
			var buffer = new InputBuffer(6);
			buffer.Add(PlayerInputAction.Attack);
			buffer.Add(PlayerInputAction.BlockParry);
			buffer.Clear();
			AssertThat(buffer.Count).IsEqual(0);
			AssertThat(buffer.Consume()).IsEqual(PlayerInputAction.None);
		}

		[TestCase]
		public void Count_Tracks_Queued_Items()
		{
			var buffer = new InputBuffer(6);
			AssertThat(buffer.Count).IsEqual(0);
			buffer.Add(PlayerInputAction.Attack);
			AssertThat(buffer.Count).IsEqual(1);
			buffer.Add(PlayerInputAction.Dodge);
			AssertThat(buffer.Count).IsEqual(2);
			buffer.Consume();
			AssertThat(buffer.Count).IsEqual(1);
		}
	}
}
