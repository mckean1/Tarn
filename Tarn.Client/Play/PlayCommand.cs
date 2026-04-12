using Tarn.ClientApp.Play.App;
using Tarn.Domain;

namespace Tarn.ClientApp.Play;

public static class PlayCommand
{
    public static int Run(string storagePath)
    {
        if (!File.Exists(storagePath))
        {
            Console.WriteLine("No saved world found. Run `new-world` first.");
            return 1;
        }

        var world = WorldStorage.Load(storagePath);
        var app = new PlayApp(storagePath, world);
        app.Run();
        return 0;
    }
}
