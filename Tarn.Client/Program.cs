using Tarn.Domain;

var storagePath = Path.Combine(AppContext.BaseDirectory, "tarn-world.json");

if (args.Length == 0)
{
    PrintUsage();
    return;
}

var command = args[0].ToLowerInvariant();

switch (command)
{
    case "match":
        RunSingleMatch(args);
        return;
    case "new-world":
        CreateWorld(storagePath, args);
        return;
}

if (!File.Exists(storagePath))
{
    Console.WriteLine("No saved world found. Run `new-world` first.");
    return;
}

var world = WorldStorage.Load(storagePath);
var simulator = new WorldSimulator();
var human = world.Players.Values.Single(player => player.IsHuman);

switch (command)
{
    case "status":
        PrintStatus(world, human);
        break;
    case "league":
        PrintLeague(world, ParseLeague(args.ElementAtOrDefault(1)) ?? human.League);
        break;
    case "schedule":
        PrintSchedule(world, args.Length > 1 ? int.Parse(args[1]) : world.Season.CurrentWeek);
        break;
    case "collection":
        PrintCollection(world, human);
        break;
    case "deck":
        HandleDeck(world, human, args.Skip(1).ToArray());
        break;
    case "collector":
        PrintCollector(world);
        break;
    case "buy-single":
        Require(args.Length >= 2, "Usage: buy-single <listingId>");
        Console.WriteLine(CollectorService.BuySingle(world, human.Id, args[1]) ? "Bought single." : "Could not buy single.");
        break;
    case "buy-pack":
        Require(args.Length >= 2, "Usage: buy-pack <productId>");
        var packCards = CollectorService.BuyPack(world, human.Id, args[1], world.Season.CurrentWeek * 17);
        Console.WriteLine(packCards.Count == 0
            ? "Could not buy pack."
            : $"Opened pack and received: {string.Join(", ", packCards.Select(card => world.GetLatestDefinition(card.CardId).Name))}");
        break;
    case "sell-collector":
        Require(args.Length >= 2, "Usage: sell-collector <instanceId>");
        Console.WriteLine(CollectorService.SellToCollector(world, human.Id, args[1]) ? "Sold card to Collector." : "Could not sell that card.");
        break;
    case "market":
        PrintMarket(world);
        break;
    case "auction":
        Require(args.Length >= 3, "Usage: auction <instanceId> <minimumBid>");
        var listing = MarketService.CreateAuctionListing(world, human.Id, args[1], int.Parse(args[2]));
        Console.WriteLine(listing is null ? "Could not create listing." : $"Listed {listing.CardId} as {listing.Id}.");
        break;
    case "bid":
        Require(args.Length >= 3, "Usage: bid <listingId> <amount>");
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
        PrintUsage();
        return;
}

WorldStorage.Save(world, storagePath);

static void CreateWorld(string path, string[] args)
{
    var name = args.Length > 1 ? args[1] : "You";
    var world = new WorldFactory().CreateNewWorld(1, name);
    WorldStorage.Save(world, path);
    Console.WriteLine($"Created Tarn world for {name} at {path}.");
}

static void RunSingleMatch(string[] args)
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

static void PrintStatus(World world, Player human)
{
    Console.WriteLine($"Year {world.Season.Year}, Week {world.Season.CurrentWeek}");
    Console.WriteLine($"You are {human.Name} in {human.League}, {world.Divisions[human.DivisionId].Name}.");
    Console.WriteLine($"Cash: {human.Cash}");
    Console.WriteLine($"Standard sets: {string.Join(", ", world.StandardSetIds)}");
    var standing = world.Season.Standings[human.Id];
    Console.WriteLine($"Record: {standing.Wins}-{standing.Losses}, Points {standing.MatchPoints}, Game Diff {standing.GameDifferential}");
}

static void PrintLeague(World world, LeagueTier league)
{
    Console.WriteLine($"{league} standings");
    foreach (var entry in StandingsCalculator.Rank(world.Season.Standings.Values.Where(item => item.League == league).ToList()))
    {
        Console.WriteLine($"{entry.LeagueRank,2}. {world.Players[entry.DeckId].Name,-18} {entry.Wins,2}-{entry.Losses,-2} Pts {entry.MatchPoints,2} GD {entry.GameDifferential,3}");
    }
}

static void PrintSchedule(World world, int week)
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

static void PrintCollection(World world, Player player)
{
    Console.WriteLine($"Collection for {player.Name}");
    foreach (var group in player.Collection
                 .OrderBy(card => world.GetLatestDefinition(card.CardId).Type)
                 .ThenBy(card => world.GetLatestDefinition(card.CardId).Rarity)
                 .ThenBy(card => world.GetLatestDefinition(card.CardId).Name, StringComparer.Ordinal)
                 .GroupBy(card => card.CardId, StringComparer.Ordinal))
    {
        var definition = world.GetLatestDefinition(group.Key);
        Console.WriteLine($"{definition.Name,-24} {definition.Type,-8} {definition.Rarity,-10} x{group.Count()} [{string.Join(", ", group.Select(card => card.InstanceId))}]");
    }
}

static void HandleDeck(World world, Player player, string[] args)
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
            foreach (var instanceId in player.ActiveDeck.NonChampionInstanceIds)
            {
                var card = world.GetLatestDefinition(player.Collection.First(entry => entry.InstanceId == instanceId).CardId);
                Console.WriteLine($"- {card.Name} ({instanceId})");
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

static string SaveDeck(Player player, World world, SubmittedDeck deck)
{
    var validation = DeckValidator.ValidateSubmittedDeck(world, player, deck);
    if (!validation.IsValid)
    {
        return $"Deck invalid: {string.Join("; ", validation.Errors)}";
    }

    player.ActiveDeck = deck;
    return "Built and saved an automatic legal deck.";
}

static void PrintCollector(World world)
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

static void PrintMarket(World world)
{
    Console.WriteLine("Active auctions");
    foreach (var listing in world.MarketListings.Where(item => item.Status == ListingStatus.Active))
    {
        var card = world.GetLatestDefinition(listing.CardId);
        var topBid = listing.Bids.Count == 0 ? "none" : listing.Bids.MaxBy(bid => bid.Amount)!.Amount.ToString();
        Console.WriteLine($"{listing.Id}: {card.Name,-24} min {listing.MinimumBid,4} top {topBid,4} seller {world.Players[listing.SellerPlayerId!].Name}");
    }
}

static LeagueTier? ParseLeague(string? value)
{
    return Enum.TryParse<LeagueTier>(value, true, out var league) ? league : null;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void PrintUsage()
{
    Console.WriteLine("Tarn CLI");
    Console.WriteLine("  new-world [name]");
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
