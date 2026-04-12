namespace Tarn.ClientApp;

public static class ClientPaths
{
    public static string GetWorldStoragePath() => Path.Combine(AppContext.BaseDirectory, "tarn-world.json");
}
