using Tarn.Domain;
using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp;

public static class CliCommands
{
    public static void CreateWorld(string path, string[] args)
    {
        var name = args.Length > 1 ? args[1] : "You";
        var world = new WorldFactory().CreateNewWorld(1, name);
        WorldStorage.Save(world, path);
        Console.WriteLine($"Created Tarn world for {name} at {path}.");
    }

    public static void RunSingleMatch(string[] args)
    {
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
    }

    public static void PrintStatus(World world, Player human)
    {
        Console.WriteLine($"Year {world.Season.Year}, Week {world.Season.CurrentWeek}");
        Console.WriteLine($"You are {human.Name} in {human.League}, {world.Divisions[human.DivisionId].Name}.");
        Console.WriteLine($"Cash: {human.Cash}");
        Console.WriteLine($"Standard sets: {string.Join(", ", world.StandardSetIds)}");
        var standing = world.Season.Standings[human.Id];
        Console.WriteLine($"Record: {standing.Wins}-{standing.Losses}, Points {standing.MatchPoints}, Game Diff {standing.GameDifferential}");
    }

    public static void PrintLeague(World world, LeagueTier league)
    {
        Console.WriteLine($"{league} standings");
        foreach (var entry in StandingsCalculator.Rank(world.Season.Standings.Values.Where(item => item.League == league).ToList()))
        {
            Console.WriteLine($"{entry.LeagueRank,2}. {world.Players[entry.DeckId].Name,-18} {entry.Wins,2}-{entry.Losses,-2} Pts {entry.MatchPoints,2} GD {entry.GameDifferential,3}");
        }
    }

    public static void PrintSchedule(World world, int week)
    {
        Console.WriteLine($"Week {week} schedule");
        foreach (var match in world.Season.Schedule.Where(match => match.Week == week).OrderBy(match => match.League).ThenBy(match => match.FixturePriority))
        {
            var resultText = match.Result is null
                ? "pending"
                : $"{world.Players[match.Result.WinnerPlayerId].Name} def. {world.Players[match.Result.LoserPlayerId].Name} {match.Result.WinnerGameWins}-{match.Result.LoserGameWins}";
            Console.WriteLine($"{match.League,-6} {world.Players[match.HomePlayerId].Name} vs {world.Players[match.AwayPlayerId].Name} [{resultText}]");
        }
    }

    public static void PrintCollection(World world, Player player)
    {
        Console.WriteLine($"Collection for {player.Name}");
        foreach (var group in CardDisplayGrouper.GroupOwnedCards(world, player.Collection))
        {
            var definition = world.GetLatestDefinition(group.CardId);
            var instanceIds = player.Collection
                .Where(card => string.Equals(card.CardId, group.CardId, StringComparison.Ordinal))
                .Select(card => card.InstanceId)
                .OrderBy(id => id, StringComparer.Ordinal);
            Console.WriteLine($"{definition.Name,-24} {definition.Type,-8} {definition.Rarity,-10} x{group.Count} [{string.Join(", ", instanceIds)}]");
        }
    }

    public static void HandleDeck(World world, Player player, string[] args)
    {
        var subCommand = args.ElementAtOrDefault(0)?.ToLowerInvariant() ?? "show";
        switch (subCommand)
        {
            case "show":
                if (player.ActiveDeck is null)
                {
                    Console.WriteLine("No active deck.");
                    return;
                }

                var champion = world.GetLatestDefinition(player.Collection.First(card => card.InstanceId == player.ActiveDeck.ChampionInstanceId).CardId);
                Console.WriteLine($"Champion: {champion.Name}");
                var cardIds = player.ActiveDeck.NonChampionInstanceIds
                    .Select(instanceId => player.Collection.First(entry => entry.InstanceId == instanceId).CardId);
                foreach (var group in CardDisplayGrouper.GroupCardIds(world, cardIds))
                {
                    Console.WriteLine($"- {group.Name} x{group.Count}");
                }
                break;
            case "auto":
                Console.WriteLine(DeckBuilder.TryBuildBestDeck(world, player, out var deck)
                    ? SaveDeck(player, world, deck!)
                    : "Could not build a legal Standard deck from your collection.");
                break;
            default:
                Console.WriteLine("Usage: deck [show|auto]");
                break;
        }
    }

    public static void PrintCollector(World world)
    {
        Console.WriteLine("Collector singles");
        foreach (var item in world.CollectorInventory.Singles.OrderBy(item => item.Price))
        {
            var definition = world.GetLatestDefinition(item.CardId);
            Console.WriteLine($"{item.ListingId}: {definition.Name,-24} {definition.Rarity,-10} {item.Price,4}");
        }

        Console.WriteLine();
        Console.WriteLine("Collector packs");
        foreach (var pack in world.CollectorInventory.Packs.OrderBy(pack => pack.Price))
        {
            Console.WriteLine($"{pack.ProductId}: {pack.SetId} pack {pack.Price}");
        }
    }

    public static void PrintMarket(World world)
    {
        Console.WriteLine("Active auctions");
        foreach (var listing in world.MarketListings.Where(item => item.Status == ListingStatus.Active))
        {
            var card = world.GetLatestDefinition(listing.CardId);
            var topBid = listing.Bids.Count == 0 ? "none" : listing.Bids.MaxBy(bid => bid.Amount)!.Amount.ToString();
            Console.WriteLine($"{listing.Id}: {card.Name,-24} min {listing.MinimumBid,4} top {topBid,4} seller {world.Players[listing.SellerPlayerId!].Name}");
        }
    }

    public static LeagueTier? ParseLeague(string? value) =>
        Enum.TryParse<LeagueTier>(value, true, out var league) ? league : null;

    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Tarn CLI");
        Console.WriteLine("  new-world [name]");
        Console.WriteLine("  play");
        Console.WriteLine("  status");
        Console.WriteLine("  league [Bronze|Silver|Gold|World]");
        Console.WriteLine("  schedule [week]");
        Console.WriteLine("  collection");
        Console.WriteLine("  deck [show|auto]");
        Console.WriteLine("  collector");
        Console.WriteLine("  buy-single <listingId>");
        Console.WriteLine("  buy-pack <productId>");
        Console.WriteLine("  sell-collector <instanceId>");
        Console.WriteLine("  market");
        Console.WriteLine("  auction <instanceId> <minimumBid>");
        Console.WriteLine("  bid <listingId> <amount>");
        Console.WriteLine("  step [count]");
        Console.WriteLine("  match --seed <number>");
    }

    private static string SaveDeck(Player player, World world, SubmittedDeck deck)
    {
        var validation = DeckValidator.ValidateSubmittedDeck(world, player, deck);
        if (!validation.IsValid)
        {
            return $"Deck invalid: {string.Join("; ", validation.Errors)}";
        }

        player.ActiveDeck = deck;
        return "Built and saved an automatic legal deck.";
    }
}
