using System.Collections.Generic;

namespace UnityChess {
	/// <summary>Base class for any chess piece.</summary>
	public abstract class Piece {
		public Side Owner { get; protected set; }

		protected Piece(Side owner) {
			Owner = owner;
		}

		public abstract Piece DeepCopy();

		public abstract Dictionary<(Square, Square), Movement> CalculateLegalMoves(
			Board board,
			GameConditions gameConditions,
			Square position
		);
		
		public override string ToString() => $"{Owner} {GetType().Name}";

		public string ToTextArt() => this switch {
			Bishop { Owner: Side.White } => "♝",
			Bishop { Owner: Side.Black } => "♗",
			King { Owner: Side.White } => "♚",
			King { Owner: Side.Black } => "♔",
			Knight { Owner: Side.White } => "♞",
			Knight { Owner: Side.Black } => "♘",
			Queen { Owner: Side.White } => "♛",
			Queen { Owner: Side.Black } => "♕",
			Pawn { Owner: Side.White } => "♟",
			Pawn { Owner: Side.Black } => "♙",
			Rook { Owner: Side.White } => "♜",
			Rook { Owner: Side.Black } => "♖",
			_ => "."
		};
	}

	public abstract class Piece<T> : Piece where T : Piece<T>, new() {
		protected Piece(Side owner) : base(owner) { }
		
		public override Piece DeepCopy() {
			return new T {
				Owner = Owner
			};
		}
	}
}