using System.ComponentModel;

namespace UnityChess {
	/// <summary>Used to describe which side's turn it currently is, and which side a piece belongs to.</summary>
	public enum Side {
		None,
		Black,
		White
	}

	public static class SideMethods {
		public static Side Complement(this Side side) => side switch {
			Side.White => Side.Black,
			Side.Black => Side.White,
			_ => throw new InvalidEnumArgumentException(nameof(side), (int) side, typeof(Side))
		};
		
		public static int ForwardDirection(this Side side) => side switch {
			Side.White => 1,
			Side.Black => -1,
			_ => throw new InvalidEnumArgumentException(nameof(side), (int) side, typeof(Side))
		};
		
		public static int CastlingRank(this Side side) => side switch {
			Side.White => 1,
			Side.Black => 8,
			_ => throw new InvalidEnumArgumentException(nameof(side), (int) side, typeof(Side))
		};
		
		public static int PawnRank(this Side side) => side switch {
			Side.White => 2,
			Side.Black => 7,
			_ => throw new InvalidEnumArgumentException(nameof(side), (int) side, typeof(Side))
		};
	}
}