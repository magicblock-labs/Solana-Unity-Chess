namespace UnityChess {
	public interface IGameSerializer {
		string Serialize(Game game);

		Game Deserialize(string gameString);
	}
}