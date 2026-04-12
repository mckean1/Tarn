using Tarn.ClientApp.Play;
using Tarn.Domain;

namespace Tarn.ClientApp;

public static class ClientHost
{
    public static int Run(string[] args)
    {
        var storagePath = ClientPaths.GetWorldStoragePath();

        if (args.Length == 0)
        {
            CliCommands.PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        switch (command)
        {
            case "match":
                CliCommands.RunSingleMatch(args);
                return 0;
            case "new-world":
                CliCommands.CreateWorld(storagePath, args);
                return 0;
            case "play":
                return PlayCommand.Run(storagePath);
        }

        if (!File.Exists(storagePath))
        {
            Console.WriteLine("No saved world found. Run `new-world` first.");
            return 1;
        }

        var world = WorldStorage.Load(storagePath);
        var simulator = new WorldSimulator();
        var human = world.Players.Values.Single(player => player.IsHuman);

        switch (command)
        {
            case "status":
                CliCommands.PrintStatus(world, human);
                break;
            case "league":
                CliCommands.PrintLeague(world, CliCommands.ParseLeague(args.ElementAtOrDefault(1)) ?? human.League);
                break;
            case "schedule":
                CliCommands.PrintSchedule(world, args.Length > 1 ? int.Parse(args[1]) : world.Season.CurrentWeek);
                break;
            case "collection":
                CliCommands.PrintCollection(world, human);
                break;
            case "deck":
                CliCommands.HandleDeck(world, human, args.Skip(1).ToArray());
                break;
            case "collector":
                CliCommands.PrintCollector(world);
                break;
            case "buy-single":
                CliCommands.Require(args.Length >= 2, "Usage: buy-single <listingId>");
                Console.WriteLine(CollectorService.BuySingle(world, human.Id, args[1]) ? "Bought single." : "Could not buy single.");
                break;
            case "buy-pack":
                CliCommands.Require(args.Length >= 2, "Usage: buy-pack <productId>");
                var packCards = CollectorService.BuyPack(world, human.Id, args[1], world.Season.CurrentWeek * 17);
                Console.WriteLine(packCards.Count == 0
                    ? "Could not buy pack."
                    : $"Opened pack and received: {string.Join(", ", packCards.Select(card => world.GetLatestDefinition(card.CardId).Name))}");
                break;
            case "sell-collector":
                CliCommands.Require(args.Length >= 2, "Usage: sell-collector <instanceId>");
                Console.WriteLine(CollectorService.SellToCollector(world, human.Id, args[1]) ? "Sold card to Collector." : "Could not sell that card.");
                break;
            case "market":
                CliCommands.PrintMarket(world);
                break;
            case "auction":
                CliCommands.Require(args.Length >= 3, "Usage: auction <instanceId> <minimumBid>");
                var listing = MarketService.CreateAuctionListing(world, human.Id, args[1], int.Parse(args[2]));
                Console.WriteLine(listing is null ? "Could not create listing." : $"Listed {listing.CardId} as {listing.Id}.");
                break;
            case "bid":
                CliCommands.Require(args.Length >= 3, "Usage: bid <listingId> <amount>");
                Console.WriteLine(MarketService.PlaceBid(world, human.Id, args[1], int.Parse(args[2])) ? "Bid placed." : "Could not place bid.");
                break;
            case "step":
                var count = args.Length > 1 ? int.Parse(args[1]) : 1;
                for (var index = 0; index < count; index++)
                {
                    simulator.StepWeek(world, (world.Season.Year * 1000) + world.Season.CurrentWeek + index);
                }

                Console.WriteLine($"Advanced {count} week(s). It is now Year {world.Season.Year}, Week {world.Season.CurrentWeek}.");
                break;
            default:
                CliCommands.PrintUsage();
                return 1;
        }

        WorldStorage.Save(world, storagePath);
        return 0;
    }
}
