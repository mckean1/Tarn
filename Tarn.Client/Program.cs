using Tarn.Domain;

if (args.Length == 0 || !string.Equals(args[0], "match", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Usage: dotnet run --project Tarn.Client -- match --seed <number>");
    return;
}

var seed = 123;
for (var index = 1; index < args.Length; index++)
{
    if (string.Equals(args[index], "--seed", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
    {
        seed = int.Parse(args[index + 1]);
        index++;
    }
}

var engine = new GameEngine();
var result = engine.RunRandomMatch(seed);

Console.Write(result.ReplayText);
