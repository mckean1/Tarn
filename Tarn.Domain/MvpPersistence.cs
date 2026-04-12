using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tarn.Domain;

public static class WorldStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Save(World world, string path)
    {
        var json = JsonSerializer.Serialize(world, Options);
        File.WriteAllText(path, json);
    }

    public static World Load(string path)
    {
        var json = File.ReadAllText(path);
        var world = JsonSerializer.Deserialize<World>(json, Options);
        return world ?? throw new InvalidOperationException("Failed to deserialize Tarn world.");
    }
}
