using System;
using System.IO;
using System.Text.Json;

namespace Minecraftonia.Game;

public static class GameSaveService
{
    private const string AppFolderName = "Minecraftonia";
    private const string SavesFolderName = "Saves";
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public static string GetSavesDirectory()
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string path = Path.Combine(basePath, AppFolderName, SavesFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetSavePath(string saveName)
    {
        if (string.IsNullOrWhiteSpace(saveName))
        {
            throw new ArgumentException("Save name must not be empty.", nameof(saveName));
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            saveName = saveName.Replace(c, '_');
        }

        return Path.Combine(GetSavesDirectory(), saveName + ".json");
    }

    public static void Save(GameSaveData saveData, string path)
    {
        if (saveData is null) throw new ArgumentNullException(nameof(saveData));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path must not be empty.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(stream, saveData, s_jsonOptions);
    }

    public static GameSaveData Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path must not be empty.", nameof(path));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Save file not found.", path);
        }

        using FileStream stream = File.OpenRead(path);
        var save = JsonSerializer.Deserialize<GameSaveData>(stream, s_jsonOptions);
        if (save is null)
        {
            throw new InvalidOperationException("Failed to load save file.");
        }

        return save;
    }
}
