namespace Minecraftonia.Game;

public interface IGameSaveService
{
    string GetSavePath(string saveName);
    GameSaveData Load(string path);
    void Save(GameSaveData data, string path);
}
