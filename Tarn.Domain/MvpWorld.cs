namespace Tarn.Domain;

public sealed class WorldFactory
{
    private static readonly string[] AiNamePool =
    [
        "Rowan", "Veyra", "Orin", "Selka", "Dain", "Mirel", "Kael", "Bryn", "Thorne", "Elira",
        "Garrick", "Nyra", "Corven", "Lyra", "Sable", "Tovin", "Maelis", "Rook", "Ilya", "Kestrel",
    ];

    private readonly TarnConfig config;
    private readonly CardGenerator generator;

    public WorldFactory(TarnConfig? config = null)
    {
        this.config = config ?? TarnConfig.Default;
        generator = new CardGenerator(this.config);
    }

    public World CreateNewWorld(int seed = 1, string humanPlayerName = "You")
    {
        var world = new World
        {
            Config = config,
            Season = new Season
            {
                Year = 1,
                CurrentWeek = 1,
                StatsLocked = false,
            },
            NewestSetReleaseYear = 1,
            NewestSetReleaseWeek = 1,
        };

        BootstrapSets(world);
        BootstrapLeaguesAndPlayers(world, humanPlayerName);
        BootstrapCollections(world, seed);
        world.Season.Schedule.AddRange(ScheduleBuilder.BuildRegularSeason(world));
        CollectorService.Refresh(world, seed);
        return world;
    }

    private void BootstrapSets(World world)
    {
        for (var sequence = 1; sequence <= config.Season.StandardRotationDepth; sequence++)
        {
            var set = generator.GenerateSet(sequence);
            var versions = generator.GenerateVersionsForSet(set);
            world.CardSets[set.Id] = set;
            world.StandardSetIds.Add(set.Id);
            foreach (var version in versions)
            {
                world.CardVersions[version.CardId] = [version];
            }
        }
    }

    private void BootstrapLeaguesAndPlayers(World world, string humanPlayerName)
    {
        var playerCounter = 1;
        for (var leagueIndex = 0; leagueIndex < config.Leagues.LeagueOrder.Count; leagueIndex++)
        {
            var tier = config.Leagues.LeagueOrder[leagueIndex];
            var league = new LeagueState { Tier = tier };
            world.Leagues[tier] = league;
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var aiIndexInLeague = 0;

            for (var divisionIndex = 0; divisionIndex < config.Leagues.DivisionsPerLeague; divisionIndex++)
            {
                var divisionId = $"{tier}-DIV{divisionIndex + 1}";
                var division = new Division
                {
                    Id = divisionId,
                    Name = $"{tier} Division {divisionIndex + 1}",
                    League = tier,
                };
                world.Divisions[divisionId] = division;
                league.DivisionIds.Add(divisionId);

                for (var slot = 0; slot < config.Leagues.PlayersPerDivision; slot++)
                {
                    var playerId = $"PLY{playerCounter:000}";
                    var isHuman = playerCounter == 1;
                    var playerName = isHuman
                        ? humanPlayerName
                        : ResolveAiPlayerName(leagueIndex, aiIndexInLeague++, usedNames);
                    usedNames.Add(playerName);
                    var player = new Player
                    {
                        Id = playerId,
                        Name = playerName,
                        League = tier,
                        DivisionId = divisionId,
                        Cash = config.Economy.StartingCash,
                        IsHuman = isHuman,
                    };
                    world.Players[playerId] = player;
                    division.PlayerIds.Add(playerId);
                    world.Season.Standings[playerId] = new StandingsEntry
                    {
                        DeckId = playerId,
                        League = tier,
                    };
                    playerCounter++;
                }
            }
        }
    }

    private static string ResolveAiPlayerName(int leagueIndex, int aiIndexInLeague, ISet<string> usedNames)
    {
        var startIndex = (leagueIndex * 5) % AiNamePool.Length;
        for (var offset = 0; offset < AiNamePool.Length; offset++)
        {
            var candidate = AiNamePool[(startIndex + aiIndexInLeague + offset) % AiNamePool.Length];
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }

        var baseName = AiNamePool[(startIndex + aiIndexInLeague) % AiNamePool.Length];
        var suffix = 2;
        while (!usedNames.Add($"{baseName} {suffix}"))
        {
            suffix++;
        }

        return $"{baseName} {suffix}";
    }

    private void BootstrapCollections(World world, int seed)
    {
        var rng = new SeededRng(seed);
        var standardCards = world.StandardSetIds
            .SelectMany(setId => world.CardSets[setId].CardIds)
            .Select(cardId => world.GetLatestDefinition(cardId))
            .ToList();

        var champions = standardCards.Where(card => card.Type == CardType.Champion && card.Rarity != CardRarity.Legendary).ToList();
        var nonChampions = standardCards.Where(card => card.Type != CardType.Champion && card.Rarity != CardRarity.Legendary).ToList();

        foreach (var player in world.Players.Values.OrderBy(player => player.Id, StringComparer.Ordinal))
        {
            var champion = champions[rng.NextInt(champions.Count)];
            GrantCard(world, player, champion.Id);

            var groupedSupport = nonChampions
                .Where(card => string.Equals(card.SetId, champion.SetId, StringComparison.Ordinal))
                .Take(15)
                .ToList();

            foreach (var card in groupedSupport.Take(10))
            {
                GrantCard(world, player, card.Id);
                GrantCard(world, player, card.Id);
                GrantCard(world, player, card.Id);
            }

            foreach (var extraChampion in champions.Take(4))
            {
                GrantCard(world, player, extraChampion.Id);
            }

            SubmittedDeck? deck = null;
            while (!DeckBuilder.TryBuildBestDeck(world, player, out deck))
            {
                var card = nonChampions[rng.NextInt(nonChampions.Count)];
                GrantCard(world, player, card.Id);
            }

            player.ActiveDeck = deck;
        }
    }

    public static OwnedCard GrantCard(World world, Player player, string cardId)
    {
        var version = world.GetLatestVersion(cardId);
        var instance = new OwnedCard
        {
            InstanceId = $"OWN{world.NextCardInstanceNumber++:000000}",
            CardId = cardId,
            Version = version.Version,
        };
        player.Collection.Add(instance);
        if (version.Definition.Rarity == CardRarity.Legendary)
        {
            world.CollectorInventory.LegendaryStates[cardId] = LegendaryState.Owned;
            world.CardSets[version.Definition.SetId].UnissuedLegendaryIds.Remove(cardId);
            world.CardSets[version.Definition.SetId].HiddenCollectorLegendaryIds.Remove(cardId);
        }

        return instance;
    }
}

public static class ScheduleBuilder
{
    private static readonly IReadOnlyList<(int left, int right)>[] AbstractPattern = BuildAbstractPattern();

    public static IReadOnlyList<Match> BuildRegularSeason(World world)
    {
        var matches = new List<Match>();
        foreach (var league in world.Leagues.Values)
        {
            var playerIds = league.DivisionIds.SelectMany(divisionId => world.Divisions[divisionId].PlayerIds).OrderBy(id => id, StringComparer.Ordinal).ToList();
            var fixturePriority = 1;

            for (var weekIndex = 0; weekIndex < AbstractPattern.Length; weekIndex++)
            {
                foreach (var pair in AbstractPattern[weekIndex])
                {
                    var home = playerIds[pair.left];
                    var away = playerIds[pair.right];
                    matches.Add(CreateMatch(world, league.Tier, world.Players[home].DivisionId, weekIndex + 1, home, away, fixturePriority++));
                }
            }
        }

        return matches.OrderBy(match => match.Week).ThenBy(match => match.League).ThenBy(match => match.FixturePriority).ToList();
    }

    private static IReadOnlyList<(int left, int right)>[] BuildAbstractPattern()
    {
        var divisions = Enumerable.Range(0, 20).ToDictionary(index => index, index => index / 5);
        var requirements = new int[20, 20];
        for (var left = 0; left < 20; left++)
        {
            for (var right = left + 1; right < 20; right++)
            {
                requirements[left, right] = divisions[left] == divisions[right] ? 3 : 1;
            }
        }

        var weeks = new List<IReadOnlyList<(int left, int right)>>();
        if (!TryBuildWeeks(0, requirements, weeks, new HashSet<string>(StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Could not generate a valid Tarn regular-season schedule.");
        }

        return weeks.ToArray();
    }

    private static bool TryBuildWeeks(
        int weekIndex,
        int[,] requirements,
        List<IReadOnlyList<(int left, int right)>> weeks,
        HashSet<string> failedStates)
    {
        if (weekIndex == 27)
        {
            return RemainingTotal(requirements) == 0;
        }

        if (!DegreesMatchRemainingWeeks(requirements, 27 - weekIndex))
        {
            return false;
        }

        var stateKey = Serialize(requirements, weekIndex);
        if (failedStates.Contains(stateKey))
        {
            return false;
        }

        var weeklyMatches = new List<(int left, int right)>();
        if (TryBuildWeekMatches(0, 0, requirements, weeklyMatches, weeks, weekIndex, failedStates))
        {
            return true;
        }

        failedStates.Add(stateKey);
        return false;
    }

    private static bool TryBuildWeekMatches(
        int usedMask,
        int depth,
        int[,] requirements,
        List<(int left, int right)> currentWeek,
        List<IReadOnlyList<(int left, int right)>> weeks,
        int weekIndex,
        HashSet<string> failedStates)
    {
        if (usedMask == (1 << 20) - 1)
        {
            weeks.Add(currentWeek.ToList());
            if (TryBuildWeeks(weekIndex + 1, requirements, weeks, failedStates))
            {
                return true;
            }

            weeks.RemoveAt(weeks.Count - 1);
            return false;
        }

        var remainingPlayer = Enumerable.Range(0, 20)
            .Where(index => (usedMask & (1 << index)) == 0)
            .OrderBy(index => AvailableOpponents(index, usedMask, requirements).Count)
            .ThenBy(index => index)
            .First();

        var opponents = AvailableOpponents(remainingPlayer, usedMask, requirements)
            .OrderByDescending(index => GetRequirement(requirements, remainingPlayer, index))
            .ThenBy(index => index)
            .ToList();

        foreach (var opponent in opponents)
        {
            Decrement(requirements, remainingPlayer, opponent);
            currentWeek.Add(depth % 2 == 0 ? (remainingPlayer, opponent) : (opponent, remainingPlayer));
            if (TryBuildWeekMatches(usedMask | (1 << remainingPlayer) | (1 << opponent), depth + 1, requirements, currentWeek, weeks, weekIndex, failedStates))
            {
                return true;
            }

            currentWeek.RemoveAt(currentWeek.Count - 1);
            Increment(requirements, remainingPlayer, opponent);
        }

        return false;
    }

    private static IReadOnlyList<int> AvailableOpponents(int player, int usedMask, int[,] requirements)
    {
        return Enumerable.Range(0, 20)
            .Where(other => (usedMask & (1 << other)) == 0 && other != player)
            .Where(other => GetRequirement(requirements, player, other) > 0)
            .ToList();
    }

    private static int RemainingTotal(int[,] requirements)
    {
        var total = 0;
        for (var left = 0; left < 20; left++)
        {
            for (var right = left + 1; right < 20; right++)
            {
                total += requirements[left, right];
            }
        }

        return total;
    }

    private static bool DegreesMatchRemainingWeeks(int[,] requirements, int remainingWeeks)
    {
        for (var player = 0; player < 20; player++)
        {
            var degree = 0;
            for (var other = 0; other < 20; other++)
            {
                if (player == other)
                {
                    continue;
                }

                degree += GetRequirement(requirements, player, other);
            }

            if (degree != remainingWeeks)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetRequirement(int[,] requirements, int left, int right)
    {
        var low = Math.Min(left, right);
        var high = Math.Max(left, right);
        return requirements[low, high];
    }

    private static void Decrement(int[,] requirements, int left, int right)
    {
        var low = Math.Min(left, right);
        var high = Math.Max(left, right);
        requirements[low, high]--;
    }

    private static void Increment(int[,] requirements, int left, int right)
    {
        var low = Math.Min(left, right);
        var high = Math.Max(left, right);
        requirements[low, high]++;
    }

    private static string Serialize(int[,] requirements, int weekIndex)
    {
        var values = new char[191];
        var cursor = 0;
        values[cursor++] = (char)('A' + weekIndex);
        for (var left = 0; left < 20; left++)
        {
            for (var right = left + 1; right < 20; right++)
            {
                values[cursor++] = (char)('0' + requirements[left, right]);
            }
        }

        return new string(values);
    }

    private static Match CreateMatch(World world, LeagueTier league, string divisionId, int week, string home, string away, int fixturePriority)
    {
        return new Match
        {
            Id = $"Y{world.Season.Year:000}-W{week:00}-{league}-{fixturePriority:000}",
            Year = world.Season.Year,
            Week = week,
            League = league,
            DivisionId = divisionId,
            HomePlayerId = home,
            AwayPlayerId = away,
            FixturePriority = fixturePriority,
            Phase = MatchPhase.RegularSeason,
        };
    }
}

public static class DeckBuilder
{
    public static bool TryBuildBestDeck(World world, Player player, out SubmittedDeck deck)
    {
        var standardSets = world.StandardSetIds.ToHashSet(StringComparer.Ordinal);
        var available = player.Collection
            .Where(card => !card.IsListed && !card.PendingSettlement)
            .Select(card => (instance: card, definition: world.GetLatestDefinition(card.CardId)))
            .Where(pair => standardSets.Contains(pair.definition.SetId))
            .ToList();

        var champion = available
            .Where(pair => pair.definition.Type == CardType.Champion)
            .OrderByDescending(pair => pair.definition.Speed)
            .ThenByDescending(pair => pair.definition.Rarity)
            .ThenBy(pair => pair.definition.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (champion == default)
        {
            deck = null!;
            return false;
        }

        var nonChampions = available
            .Where(pair => pair.definition.Type != CardType.Champion)
            .OrderByDescending(pair => pair.definition.Attack + pair.definition.Health + pair.definition.EffectValue)
            .ThenByDescending(pair => pair.definition.HasDefender || pair.definition.HasMagnet)
            .ThenBy(pair => pair.definition.Id, StringComparer.Ordinal)
            .ToList();

        var selected = new List<string>();
        var copies = new Dictionary<string, int>(StringComparer.Ordinal);
        var power = champion.definition.Power;
        foreach (var card in nonChampions)
        {
            copies.TryGetValue(card.definition.Id, out var copyCount);
            if (copyCount >= world.Config.Season.MaxCopiesPerCard)
            {
                continue;
            }

            if (power + card.definition.Power > world.Config.Season.MaxDeckPower)
            {
                continue;
            }

            selected.Add(card.instance.InstanceId);
            copies[card.definition.Id] = copyCount + 1;
            power += card.definition.Power;
            if (selected.Count == world.Config.Season.NonChampionCount)
            {
                break;
            }
        }

        if (selected.Count != world.Config.Season.NonChampionCount)
        {
            deck = null!;
            return false;
        }

        deck = new SubmittedDeck
        {
            PlayerId = player.Id,
            ChampionInstanceId = champion.instance.InstanceId,
            NonChampionInstanceIds = selected,
            SubmittedWeek = world.Season.CurrentWeek,
            Label = "Auto",
        };

        return DeckValidator.ValidateSubmittedDeck(world, player, deck).IsValid;
    }
}
