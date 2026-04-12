namespace Tarn.Domain;

public sealed class WorldSimulator
{
    private readonly GameEngine engine = new();

    public void StepWeek(World world, int seed)
    {
        RunAiMarketPhase(world, seed);
        MarketService.SettleWeek(world);
        EnsureDeckSubmissions(world);

        if (world.Season.CurrentWeek <= world.Config.Season.RegularSeasonWeeks)
        {
            SimulateScheduledWeek(world, world.Season.CurrentWeek, seed);
        }
        else if (world.Season.CurrentWeek == world.Config.Season.RegularSeasonWeeks + 1)
        {
            BuildPlayoffSchedule(world);
            SimulateScheduledWeek(world, world.Season.CurrentWeek, seed);
        }
        else if (world.Season.CurrentWeek <= world.Config.Season.RegularSeasonWeeks + world.Config.Season.PlayoffWeeks)
        {
            SimulateScheduledWeek(world, world.Season.CurrentWeek, seed);
        }
        else
        {
            ResolveAdministrativeWeek(world, seed);
        }

        world.Season.CurrentWeek = world.Season.CurrentWeek == world.Config.Season.TotalWeeks
            ? 1
            : world.Season.CurrentWeek + 1;
    }

    public InitiativeContext BuildInitiativeContext(Match fixture)
    {
        return new InitiativeContext(
            fixture.HomePlayerId,
            fixture.AwayPlayerId,
            fixture.FixturePriority,
            fixture.Phase == MatchPhase.Playoffs,
            fixture.HomeSeed,
            fixture.AwaySeed);
    }

    public void SimulateScheduledWeek(World world, int week, int seed)
    {
        var fixtures = world.Season.Schedule
            .Where(match => match.Week == week && match.Result is null)
            .OrderBy(match => match.League)
            .ThenBy(match => match.FixturePriority)
            .ToList();

        foreach (var fixture in fixtures)
        {
            var result = SimulateMatch(world, fixture, seed + fixture.FixturePriority);
            fixture.Result = result;
            if (fixture.Phase == MatchPhase.RegularSeason)
            {
                StandingsCalculator.ApplyMatchResult(result, world.Season.Standings);
            }
        }

        foreach (var league in world.Config.Leagues.LeagueOrder)
        {
            var ranked = StandingsCalculator.Rank(world.Season.Standings.Values.Where(entry => entry.League == league).ToList());
            if (week == world.Config.Season.RegularSeasonWeeks && ranked.Count > 0)
            {
                BuildFinalPlacementsFromRegularSeason(world, league, ranked);
            }
        }

        if (week == 28)
        {
            BuildSemifinalSchedule(world);
        }
        else if (week == 29)
        {
            BuildFinalSchedule(world);
        }
        else if (week == 30)
        {
            BuildPlayoffFinalPlacements(world);
        }
    }

    public MatchResult SimulateMatch(World world, Match fixture, int seed)
    {
        var home = world.Players[fixture.HomePlayerId];
        var away = world.Players[fixture.AwayPlayerId];
        var homeDeck = home.ActiveDeck ?? throw new InvalidOperationException($"Player '{home.Id}' has no deck.");
        var awayDeck = away.ActiveDeck ?? throw new InvalidOperationException($"Player '{away.Id}' has no deck.");
        var bestOf = fixture.PlayoffRound == MatchRoundType.Final ? 5 : fixture.Phase == MatchPhase.Playoffs ? 3 : 3;
        var targetWins = (bestOf / 2) + 1;
        var homeWins = 0;
        var awayWins = 0;
        var games = new List<GameResult>();

        for (var gameIndex = 0; homeWins < targetWins && awayWins < targetWins; gameIndex++)
        {
            var result = engine.RunMatch(BuildSetup(world, fixture, homeDeck, awayDeck, seed + gameIndex));
            if (string.Equals(result.WinnerPlayerId, "P1", StringComparison.Ordinal))
            {
                homeWins++;
                games.Add(new GameResult(homeWins, awayWins, home.Id, away.Id));
            }
            else
            {
                awayWins++;
                games.Add(new GameResult(awayWins, homeWins, away.Id, home.Id));
            }
        }

        TrackCardUsage(world, home, homeDeck, away, awayDeck, fixture, homeWins > awayWins);

        return new MatchResult
        {
            WinnerPlayerId = homeWins > awayWins ? home.Id : away.Id,
            LoserPlayerId = homeWins > awayWins ? away.Id : home.Id,
            WinnerGameWins = Math.Max(homeWins, awayWins),
            LoserGameWins = Math.Min(homeWins, awayWins),
            Games = games,
        };
    }

    public void ResolveAdministrativeWeek(World world, int seed)
    {
        if (world.Season.CurrentWeek == world.Config.Season.SeasonCloseWeek)
        {
            world.Season.StatsLocked = true;
            BuildFinalPlacements(world);
            FreezeCompletedSeasonStats(world);
            return;
        }

        if (world.Season.CurrentWeek == world.Config.Season.RewardWeek)
        {
            world.LastCompletedSeasonStats ??= new CompletedSeasonStats
            {
                SeasonYear = world.Season.Year,
                IsFrozen = true,
                CardStats = world.Season.CardStats.ToDictionary(
                    pair => pair.Key,
                    pair => CloneStats(pair.Value),
                    StringComparer.Ordinal),
            };
            PayoutRewards(world);
            ApplyPromotionRelegation(world);
            RebuildDivisions(world);
            StartNextSeasonSchedule(world);
            return;
        }

        if (world.Season.CurrentWeek == world.Config.Season.PatchWeek)
        {
            ApplySeasonalPatches(world);
            RotateStandard(world);
            AddNewSet(world);
            return;
        }

        if (world.Season.CurrentWeek == world.Config.Season.GrantWeek)
        {
            world.NewestSetReleaseYear = world.Season.Year;
            world.NewestSetReleaseWeek = world.Season.CurrentWeek;
            GrantNewSeasonCards(world, seed);
            CollectorService.Refresh(world, seed);
            world.Season.StatsLocked = false;
            return;
        }
    }

    public IReadOnlyList<(int Seed, string PlayerId)> GetPlayoffSeeds(World world, LeagueTier league)
    {
        var entries = world.Season.Standings
            .Where(pair => pair.Value.League == league)
            .Select(pair => (PlayerId: pair.Key, Entry: pair.Value))
            .ToList();
        var divisionWinners = world.Leagues[league].DivisionIds
            .Select(divisionId => world.Divisions[divisionId].PlayerIds
                .Select(playerId => (PlayerId: playerId, Entry: world.Season.Standings[playerId]))
                .OrderByDescending(pair => pair.Entry.MatchPoints)
                .ThenByDescending(pair => pair.Entry.GameDifferential)
                .ThenBy(pair => pair.PlayerId, StringComparer.Ordinal)
                .First())
            .OrderByDescending(pair => pair.Entry.MatchPoints)
            .ThenByDescending(pair => pair.Entry.GameDifferential)
            .ThenBy(pair => pair.PlayerId, StringComparer.Ordinal)
            .ToList();

        var wildCards = entries
            .Where(entry => divisionWinners.All(winner => !string.Equals(winner.PlayerId, entry.PlayerId, StringComparison.Ordinal)))
            .OrderByDescending(pair => pair.Entry.MatchPoints)
            .ThenByDescending(pair => pair.Entry.GameDifferential)
            .ThenBy(pair => pair.PlayerId, StringComparer.Ordinal)
            .Take(4)
            .ToList();

        return divisionWinners.Concat(wildCards)
            .Select((pair, index) => (index + 1, pair.PlayerId))
            .ToList();
    }

    public IReadOnlyList<(LeagueTier From, LeagueTier To, string PlayerId)> GetPromotionRelegationMoves(World world)
    {
        var moves = new List<(LeagueTier From, LeagueTier To, string PlayerId)>();
        AddMoves(LeagueTier.Bronze, LeagueTier.Silver, takeTop: 4, takeBottom: 0);
        AddMoves(LeagueTier.Silver, LeagueTier.Gold, takeTop: 4, takeBottom: 4);
        AddMoves(LeagueTier.Gold, LeagueTier.World, takeTop: 4, takeBottom: 4);
        AddMoves(LeagueTier.World, LeagueTier.Gold, takeTop: 0, takeBottom: 4);
        return moves;

        void AddMoves(LeagueTier source, LeagueTier target, int takeTop, int takeBottom)
        {
            if (!world.Season.FinalPlacements.TryGetValue(source, out var placements))
            {
                return;
            }

            if (takeTop > 0)
            {
                moves.AddRange(placements.Take(takeTop).Select(playerId => (source, target, playerId)));
            }

            if (takeBottom > 0)
            {
                moves.AddRange(placements.TakeLast(takeBottom).Select(playerId => (source, target, playerId)));
            }
        }
    }

    private MatchSetup BuildSetup(World world, Match fixture, SubmittedDeck homeDeck, SubmittedDeck awayDeck, int seed)
    {
        var homeChampion = (ChampionCardDefinition)world.GetLatestDefinition(world.Players[fixture.HomePlayerId].Collection.First(card => card.InstanceId == homeDeck.ChampionInstanceId).CardId);
        var awayChampion = (ChampionCardDefinition)world.GetLatestDefinition(world.Players[fixture.AwayPlayerId].Collection.First(card => card.InstanceId == awayDeck.ChampionInstanceId).CardId);
        var homeCards = homeDeck.NonChampionInstanceIds
            .Select(id => world.GetLatestDefinition(world.Players[fixture.HomePlayerId].Collection.First(card => card.InstanceId == id).CardId))
            .ToList();
        var awayCards = awayDeck.NonChampionInstanceIds
            .Select(id => world.GetLatestDefinition(world.Players[fixture.AwayPlayerId].Collection.First(card => card.InstanceId == id).CardId))
            .ToList();

        return new MatchSetup
        {
            Seed = seed,
            PlayerOneDeck = new DeckDefinition(homeChampion, homeCards),
            PlayerTwoDeck = new DeckDefinition(awayChampion, awayCards),
            ShuffleDecks = true,
            PlayerOneId = "P1",
            PlayerTwoId = "P2",
            Initiative = BuildInitiativeContext(fixture),
        };
    }

    private void TrackCardUsage(World world, Player home, SubmittedDeck homeDeck, Player away, SubmittedDeck awayDeck, Match fixture, bool homeWon)
    {
        var homeCards = homeDeck.NonChampionInstanceIds.Append(homeDeck.ChampionInstanceId)
            .Select(id => home.Collection.First(card => card.InstanceId == id).CardId);
        var awayCards = awayDeck.NonChampionInstanceIds.Append(awayDeck.ChampionInstanceId)
            .Select(id => away.Collection.First(card => card.InstanceId == id).CardId);

        void Apply(IEnumerable<string> cardIds, bool won)
        {
            foreach (var cardId in cardIds.Distinct(StringComparer.Ordinal))
            {
                if (!world.Season.CardStats.TryGetValue(cardId, out var stats))
                {
                    stats = new CardUsageStats { CardId = cardId };
                    world.Season.CardStats[cardId] = stats;
                }

                stats.DeckAppearances++;
                if (fixture.Phase == MatchPhase.Playoffs)
                {
                    stats.PlayoffDeckAppearances++;
                }

                if (won)
                {
                    stats.MatchWins++;
                }
                else
                {
                    stats.MatchLosses++;
                }

                stats.RoleDemand++;
            }
        }

        Apply(homeCards, homeWon);
        Apply(awayCards, !homeWon);
    }

    private void EnsureDeckSubmissions(World world)
    {
        foreach (var player in world.Players.Values)
        {
            if (player.ActiveDeck is null || !DeckValidator.ValidateSubmittedDeck(world, player, player.ActiveDeck).IsValid)
            {
                DeckBuilder.TryBuildBestDeck(world, player, out var deck);
                player.ActiveDeck = deck;
            }
        }
    }

    private void RunAiMarketPhase(World world, int seed)
    {
        var rng = new SeededRng(seed + world.Season.CurrentWeek);
        foreach (var player in world.Players.Values.Where(player => !player.IsHuman))
        {
            if (world.Season.CurrentWeek <= 8 && player.Cash >= world.Config.Economy.PackPrices.OlderStandard && world.CollectorInventory.Packs.Count > 0 && rng.NextInt(100) < 20)
            {
                CollectorService.BuyPack(world, player.Id, world.CollectorInventory.Packs[0].ProductId, seed + rng.NextInt(1000));
            }

            var single = world.CollectorInventory.Singles
                .Where(item => item.Price <= player.Cash)
                .OrderBy(item => item.Price)
                .FirstOrDefault();
            if (single is not null && rng.NextInt(100) < 15)
            {
                CollectorService.BuySingle(world, player.Id, single.ListingId);
            }

            var auction = world.MarketListings
                .Where(listing => listing.Status == ListingStatus.Active)
                .Where(listing => !string.Equals(listing.SellerPlayerId, player.Id, StringComparison.Ordinal))
                .Where(listing => MarketService.GetAvailableCashForBids(world, player.Id, listing.Id) >= MarketService.GetNextBidAmount(listing))
                .OrderBy(listing => MarketService.GetNextBidAmount(listing))
                .FirstOrDefault();
            if (auction is not null)
            {
                MarketService.PlaceBid(world, player.Id, auction.Id, MarketService.GetNextBidAmount(auction));
            }

            var duplicate = player.Collection
                .GroupBy(card => card.CardId, StringComparer.Ordinal)
                .Where(group => group.Count() > 3)
                .SelectMany(group => group.Skip(3))
                .FirstOrDefault(card => !card.IsListed && !card.PendingSettlement);
            if (duplicate is not null && rng.NextInt(100) < 25)
            {
                MarketService.CreateAuctionListing(world, player.Id, duplicate.InstanceId, CollectorService.GetCollectorBuybackPrice(world, duplicate.CardId) + 5);
            }
        }
    }

    private void BuildPlayoffSchedule(World world)
    {
        foreach (var league in world.Config.Leagues.LeagueOrder)
        {
            var seeds = GetPlayoffSeeds(world, league);
            var quarterPairs = new[] { (1, 8), (4, 5), (2, 7), (3, 6) };
            var priority = 1;
            foreach (var pair in quarterPairs)
            {
                world.Season.Schedule.Add(new Match
                {
                    Id = $"Y{world.Season.Year:000}-W28-{league}-QF{priority}",
                    Year = world.Season.Year,
                    Week = 28,
                    League = league,
                    DivisionId = world.Players[seeds.First(seed => seed.Seed == pair.Item1).PlayerId].DivisionId,
                    HomePlayerId = seeds.First(seed => seed.Seed == pair.Item1).PlayerId,
                    AwayPlayerId = seeds.First(seed => seed.Seed == pair.Item2).PlayerId,
                    FixturePriority = priority,
                    Phase = MatchPhase.Playoffs,
                    PlayoffRound = MatchRoundType.Quarterfinal,
                    HomeSeed = pair.Item1,
                    AwaySeed = pair.Item2,
                });
                priority++;
            }
        }
    }

    private void BuildSemifinalSchedule(World world)
    {
        foreach (var league in world.Config.Leagues.LeagueOrder)
        {
            var quarters = world.Season.Schedule
                .Where(match => match.League == league && match.Week == 28 && match.PlayoffRound == MatchRoundType.Quarterfinal)
                .OrderBy(match => match.FixturePriority)
                .ToList();
            if (quarters.Any(match => match.Result is null))
            {
                continue;
            }

            var winners = quarters.Select(match => new
            {
                Match = match,
                WinnerId = match.Result!.WinnerPlayerId,
                WinnerSeed = string.Equals(match.Result!.WinnerPlayerId, match.HomePlayerId, StringComparison.Ordinal) ? match.HomeSeed!.Value : match.AwaySeed!.Value,
            }).ToList();

            CreatePlayoffMatch(world, league, 29, MatchRoundType.Semifinal, 1, winners[0].WinnerId, winners[1].WinnerId, winners[0].WinnerSeed, winners[1].WinnerSeed);
            CreatePlayoffMatch(world, league, 29, MatchRoundType.Semifinal, 2, winners[2].WinnerId, winners[3].WinnerId, winners[2].WinnerSeed, winners[3].WinnerSeed);
        }
    }

    private void BuildFinalSchedule(World world)
    {
        foreach (var league in world.Config.Leagues.LeagueOrder)
        {
            var semis = world.Season.Schedule
                .Where(match => match.League == league && match.Week == 29 && match.PlayoffRound == MatchRoundType.Semifinal)
                .OrderBy(match => match.FixturePriority)
                .ToList();
            if (semis.Any(match => match.Result is null))
            {
                continue;
            }

            var winners = semis.Select(match => new
            {
                WinnerId = match.Result!.WinnerPlayerId,
                WinnerSeed = string.Equals(match.Result!.WinnerPlayerId, match.HomePlayerId, StringComparison.Ordinal) ? match.HomeSeed!.Value : match.AwaySeed!.Value,
            }).ToList();

            CreatePlayoffMatch(world, league, 30, MatchRoundType.Final, 1, winners[0].WinnerId, winners[1].WinnerId, winners[0].WinnerSeed, winners[1].WinnerSeed);
        }
    }

    private void BuildPlayoffFinalPlacements(World world)
    {
        foreach (var league in world.Config.Leagues.LeagueOrder)
        {
            var regular = world.Season.FinalPlacements.GetValueOrDefault(league)?.ToList() ?? [];
            var final = world.Season.Schedule.First(match => match.League == league && match.Week == 30 && match.PlayoffRound == MatchRoundType.Final);
            var semis = world.Season.Schedule.Where(match => match.League == league && match.Week == 29 && match.PlayoffRound == MatchRoundType.Semifinal).ToList();
            var quarters = world.Season.Schedule.Where(match => match.League == league && match.Week == 28 && match.PlayoffRound == MatchRoundType.Quarterfinal).ToList();

            var ordered = new List<string>
            {
                final.Result!.WinnerPlayerId,
                final.Result.LoserPlayerId,
            };
            ordered.AddRange(semis.Select(match => match.Result!.LoserPlayerId).OrderBy(playerId => ResolveSeed(semis, playerId)));
            ordered.AddRange(quarters.Select(match => match.Result!.LoserPlayerId).OrderBy(playerId => ResolveSeed(quarters, playerId)));
            ordered.AddRange(regular.Where(playerId => !ordered.Contains(playerId, StringComparer.Ordinal)));
            world.Season.FinalPlacements[league] = ordered;
        }

        static int ResolveSeed(IEnumerable<Match> matches, string playerId)
        {
            var match = matches.First(item => string.Equals(item.HomePlayerId, playerId, StringComparison.Ordinal) || string.Equals(item.AwayPlayerId, playerId, StringComparison.Ordinal));
            return string.Equals(match.HomePlayerId, playerId, StringComparison.Ordinal) ? match.HomeSeed ?? 99 : match.AwaySeed ?? 99;
        }
    }

    private void BuildFinalPlacementsFromRegularSeason(World world, LeagueTier league, IReadOnlyList<StandingsEntry> ranked)
    {
        world.Season.FinalPlacements[league] = ranked.Select(entry => entry.DeckId).ToList();
    }

    private void BuildFinalPlacements(World world)
    {
        foreach (var league in world.Config.Leagues.LeagueOrder)
        {
            var ranked = StandingsCalculator.Rank(world.Season.Standings.Values.Where(entry => entry.League == league).ToList());
            world.Season.FinalPlacements[league] = ranked.Select(entry => entry.DeckId).ToList();
        }
    }

    private void PayoutRewards(World world)
    {
        foreach (var league in world.Config.Leagues.LeagueOrder)
        {
            if (!world.Season.FinalPlacements.TryGetValue(league, out var placements))
            {
                continue;
            }

            for (var index = 0; index < placements.Count; index++)
            {
                var player = world.Players[placements[index]];
                player.Cash += ResolvePayout(world.Config.Economy.Payouts[league], index + 1);
            }
        }
    }

    private static int ResolvePayout(IReadOnlyList<(int start, int end, int amount)> table, int rank)
    {
        return table.First(row => rank >= row.start && rank <= row.end).amount;
    }

    private void ApplyPromotionRelegation(World world)
    {
        foreach (var move in GetPromotionRelegationMoves(world))
        {
            world.Players[move.PlayerId].League = move.To;
        }
    }

    private void RebuildDivisions(World world)
    {
        foreach (var division in world.Divisions.Values)
        {
            division.PlayerIds.Clear();
        }

        foreach (var league in world.Config.Leagues.LeagueOrder)
        {
            var players = world.Players.Values.Where(player => player.League == league).OrderBy(player => player.Id, StringComparer.Ordinal).ToList();
            for (var index = 0; index < players.Count; index++)
            {
                var divisionId = world.Leagues[league].DivisionIds[index / world.Config.Leagues.PlayersPerDivision];
                players[index].DivisionId = divisionId;
                world.Divisions[divisionId].PlayerIds.Add(players[index].Id);
            }
        }
    }

    private void StartNextSeasonSchedule(World world)
    {
        world.Season = new Season
        {
            Year = world.Season.Year + 1,
            CurrentWeek = world.Config.Season.RewardWeek,
            StatsLocked = true,
            Schedule = ScheduleBuilder.BuildRegularSeason(world).ToList(),
            Standings = world.Players.Values.ToDictionary(
                player => player.Id,
                player => new StandingsEntry
                {
                    DeckId = player.Id,
                    League = player.League,
                },
                StringComparer.Ordinal),
        };
    }

    private void CreatePlayoffMatch(World world, LeagueTier league, int week, MatchRoundType round, int priority, string playerOne, string playerTwo, int seedOne, int seedTwo)
    {
        var home = seedOne <= seedTwo ? playerOne : playerTwo;
        var away = home == playerOne ? playerTwo : playerOne;
        var homeSeed = home == playerOne ? seedOne : seedTwo;
        var awaySeed = home == playerOne ? seedTwo : seedOne;
        world.Season.Schedule.Add(new Match
        {
            Id = $"Y{world.Season.Year:000}-W{week}-{league}-{round}-{priority}",
            Year = world.Season.Year,
            Week = week,
            League = league,
            DivisionId = world.Players[home].DivisionId,
            HomePlayerId = home,
            AwayPlayerId = away,
            FixturePriority = priority,
            Phase = MatchPhase.Playoffs,
            PlayoffRound = round,
            HomeSeed = homeSeed,
            AwaySeed = awaySeed,
        });
    }

    private void ApplySeasonalPatches(World world)
    {
        var statsSource = world.LastCompletedSeasonStats?.CardStats
            ?? throw new InvalidOperationException("Completed season stats must be frozen before seasonal patching.");
        foreach (var versionList in world.CardVersions.Values)
        {
            var latest = versionList.OrderByDescending(version => version.Version).First();
            var stats = statsSource.GetValueOrDefault(latest.CardId) ?? new CardUsageStats { CardId = latest.CardId };
            var classification = CardHealthAnalyzer.Classify(stats, world.Config.PatchThresholds);
            if (classification is PatchClassification.Healthy or PatchClassification.Watchlist)
            {
                continue;
            }

            var ops = CardPatcher.BuildPatchOps(latest.Definition, classification);
            var patchedDefinition = CardPatcher.Apply(latest.Definition, latest.Version + 1, ops);
            var patchedVersion = new CardVersion(latest.CardId, latest.Version + 1, patchedDefinition, patchedDefinition.Attack, patchedDefinition.Health, patchedDefinition.Speed, patchedDefinition.Power, patchedDefinition.Keywords, patchedDefinition.RulesText);
            versionList.Add(patchedVersion);
            world.PatchHistory.Add(new PatchResult(latest.CardId, latest.Version, patchedVersion.Version, classification, ops));
        }
    }

    private void RotateStandard(World world)
    {
        while (world.StandardSetIds.Count >= world.Config.Season.StandardRotationDepth)
        {
            world.StandardSetIds.RemoveAt(0);
        }
    }

    private void AddNewSet(World world)
    {
        var generator = new CardGenerator(world.Config);
        var nextSequence = world.CardSets.Values.Max(set => set.Sequence) + 1;
        var set = generator.GenerateSet(nextSequence);
        world.CardSets[set.Id] = set;
        world.StandardSetIds.Add(set.Id);
        foreach (var version in generator.GenerateVersionsForSet(set))
        {
            world.CardVersions[version.CardId] = [version];
        }
    }

    private void GrantNewSeasonCards(World world, int seed)
    {
        var newestSetId = world.StandardSetIds.Last();
        var cards = world.CardSets[newestSetId].CardIds.Select(world.GetLatestDefinition).ToList();
        var champions = cards.Where(card => card.Type == CardType.Champion && card.Rarity != CardRarity.Legendary).OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
        var supports = cards.Where(card => card.Type != CardType.Champion && card.Rarity != CardRarity.Legendary).OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
        var rng = new SeededRng(seed + world.Season.Year);

        foreach (var player in world.Players.Values)
        {
            var champion = champions[rng.NextInt(champions.Count)];
            WorldFactory.GrantCard(world, player, champion.Id);

            foreach (var support in supports.Where(card => string.Equals(card.SetId, champion.SetId, StringComparison.Ordinal)).Take(2))
            {
                WorldFactory.GrantCard(world, player, support.Id);
            }

            for (var index = 0; index < 2; index++)
            {
                var card = supports[rng.NextInt(supports.Count)];
                WorldFactory.GrantCard(world, player, card.Id);
            }
        }
    }

    private void FreezeCompletedSeasonStats(World world)
    {
        world.LastCompletedSeasonStats = new CompletedSeasonStats
        {
            SeasonYear = world.Season.Year,
            IsFrozen = true,
            CardStats = world.Season.CardStats.ToDictionary(
                pair => pair.Key,
                pair => CloneStats(pair.Value),
                StringComparer.Ordinal),
        };
    }

    private static CardUsageStats CloneStats(CardUsageStats stats)
    {
        return new CardUsageStats
        {
            CardId = stats.CardId,
            DeckAppearances = stats.DeckAppearances,
            MatchWins = stats.MatchWins,
            MatchLosses = stats.MatchLosses,
            PlayoffDeckAppearances = stats.PlayoffDeckAppearances,
            MarketDemand = stats.MarketDemand,
            RoleDemand = stats.RoleDemand,
        };
    }
}

public static class CardHealthAnalyzer
{
    public static PatchClassification Classify(CardUsageStats stats, PatchThresholdConfig thresholds)
    {
        var totalMatches = Math.Max(1, stats.TotalMatches);
        var appearanceRate = stats.DeckAppearances / (decimal)totalMatches;
        var winDelta = totalMatches == 0 ? 0m : (stats.MatchWins - stats.MatchLosses) / (decimal)totalMatches;
        var playoffPresence = stats.PlayoffDeckAppearances / (decimal)totalMatches;
        var marketPressure = stats.MarketDemand / (decimal)totalMatches;
        var rolePressure = stats.RoleDemand / (decimal)totalMatches;

        var nerfFlags = 0;
        if (appearanceRate >= thresholds.AppearanceNerf) nerfFlags++;
        if (winDelta >= thresholds.WinDeltaNerf) nerfFlags++;
        if (playoffPresence >= thresholds.PlayoffNerf) nerfFlags++;
        if (marketPressure >= thresholds.MarketNerf) nerfFlags++;
        if (rolePressure >= thresholds.RoleNerf) nerfFlags++;

        var buffFlags = 0;
        if (appearanceRate <= thresholds.AppearanceBuff) buffFlags++;
        if (winDelta <= thresholds.WinDeltaBuff) buffFlags++;
        if (playoffPresence <= thresholds.PlayoffBuff) buffFlags++;
        if (marketPressure <= thresholds.MarketBuff) buffFlags++;
        if (rolePressure <= thresholds.RoleBuff) buffFlags++;

        if (nerfFlags >= 4) return PatchClassification.HardNerf;
        if (nerfFlags >= 2) return PatchClassification.Nerf;
        if (buffFlags >= 4) return PatchClassification.HardBuff;
        if (buffFlags >= 2) return PatchClassification.Buff;
        return stats.DeckAppearances == 0 ? PatchClassification.Watchlist : PatchClassification.Healthy;
    }
}

public static class CardPatcher
{
    public static IReadOnlyList<CardPatchOp> BuildPatchOps(CardDefinition definition, PatchClassification classification)
    {
        var ops = new List<CardPatchOp>();
        switch (classification)
        {
            case PatchClassification.Buff:
                ops.Add(definition.Type == CardType.Unit
                    ? new CardPatchOp(PatchOpType.HealthDelta, 2, Description: "+2 Health")
                    : new CardPatchOp(PatchOpType.EffectValueDelta, 1, Description: "Increase main effect by 1"));
                break;
            case PatchClassification.HardBuff:
                ops.Add(new CardPatchOp(PatchOpType.HealthDelta, 3, Description: "+3 Health"));
                ops.Add(new CardPatchOp(PatchOpType.AttackDelta, definition.Type == CardType.Unit ? 1 : 0, Description: definition.Type == CardType.Unit ? "+1 Attack" : "No attack change"));
                break;
            case PatchClassification.Nerf:
                ops.Add(definition.Type == CardType.Unit
                    ? new CardPatchOp(PatchOpType.AttackDelta, -1, Description: "-1 Attack")
                    : new CardPatchOp(PatchOpType.EffectValueDelta, -1, Description: "Reduce main effect by 1"));
                break;
            case PatchClassification.HardNerf:
                ops.Add(new CardPatchOp(PatchOpType.AttackDelta, -2, Description: "-2 Attack"));
                ops.Add(new CardPatchOp(PatchOpType.AddOncePerRoundLimiter, Description: "Add once per round limiter"));
                break;
        }

        return ops.Take(2).Where(op => op.NumericDelta is null || op.NumericDelta != 0 || op.Type == PatchOpType.AddOncePerRoundLimiter).ToList();
    }

    public static CardDefinition Apply(CardDefinition definition, int newVersion, IReadOnlyList<CardPatchOp> operations)
    {
        var updated = definition with { Version = newVersion };
        foreach (var operation in operations)
        {
            updated = operation.Type switch
            {
                PatchOpType.AttackDelta => updated with { Attack = Math.Max(0, updated.Attack + operation.NumericDelta!.Value) },
                PatchOpType.HealthDelta => updated with { Health = Math.Max(1, updated.Health + operation.NumericDelta!.Value) },
                PatchOpType.SpeedDelta => updated with { Speed = Math.Max(0, updated.Speed + operation.NumericDelta!.Value) },
                PatchOpType.PowerDelta => updated with { Power = Math.Max(0, updated.Power + operation.NumericDelta!.Value) },
                PatchOpType.EffectValueDelta => updated with { EffectValue = Math.Max(0, updated.EffectValue + operation.NumericDelta!.Value) },
                PatchOpType.AddKeyword when operation.Keyword is not null => updated with { Keywords = updated.Keywords.Append(operation.Keyword).Distinct(StringComparer.Ordinal).ToList() },
                PatchOpType.RemoveKeyword when operation.Keyword is not null => updated with { Keywords = updated.Keywords.Where(keyword => !string.Equals(keyword, operation.Keyword, StringComparison.Ordinal)).ToList() },
                PatchOpType.AddOncePerRoundLimiter => updated with { OncePerRound = true },
                _ => updated,
            };
        }

        return updated with { RulesText = updated.RulesText };
    }
}
