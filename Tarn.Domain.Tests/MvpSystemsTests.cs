using Tarn.Domain;

namespace Tarn.Domain.Tests;

public sealed class MvpSystemsTests
{
    [Fact]
    public void DeckValidator_RejectsTooManyCopies()
    {
        var world = new WorldFactory().CreateNewWorld();
        var player = world.Players.Values.Single(item => item.IsHuman);
        var champion = player.Collection.First(card => world.GetLatestDefinition(card.CardId).Type == CardType.Champion);
        var repeatedCardId = player.Collection
            .Where(card => world.GetLatestDefinition(card.CardId).Type != CardType.Champion)
            .GroupBy(card => card.CardId, StringComparer.Ordinal)
            .First()
            .Key;
        WorldFactory.GrantCard(world, player, repeatedCardId);
        var repeated = player.Collection
            .Where(card => string.Equals(card.CardId, repeatedCardId, StringComparison.Ordinal))
            .Take(4)
            .Select(card => card.InstanceId)
            .ToList();
        var filler = player.Collection
            .Where(card => world.GetLatestDefinition(card.CardId).Type != CardType.Champion && !repeated.Contains(card.InstanceId, StringComparer.Ordinal))
            .Take(26)
            .Select(card => card.InstanceId)
            .ToList();

        var deck = new SubmittedDeck
        {
            PlayerId = player.Id,
            ChampionInstanceId = champion.InstanceId,
            NonChampionInstanceIds = repeated.Concat(filler).ToList(),
            SubmittedWeek = world.Season.CurrentWeek,
        };

        var result = DeckValidator.ValidateSubmittedDeck(world, player, deck);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("copy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FixturePriorityBreaksChampionSpeedTiesInRegularSeason()
    {
        var generator = new CardGenerator();
        var set = generator.GenerateSet(1);
        var versions = generator.GenerateVersionsForSet(set);
        var championA = (ChampionCardDefinition)versions.First(version => version.Definition.Type == CardType.Champion).Definition with { Speed = 5 };
        var championB = (ChampionCardDefinition)versions.Skip(1).First(version => version.Definition.Type == CardType.Champion).Definition with { Speed = 5 };
        var filler = versions.Where(version => version.Definition.Type != CardType.Champion).Take(30).Select(version => version.Definition).ToList();
        var setup = new MatchSetup
        {
            Seed = 1,
            PlayerOneDeck = new DeckDefinition(championA, filler),
            PlayerTwoDeck = new DeckDefinition(championB, filler),
            ShuffleDecks = false,
            Initiative = new InitiativeContext("P1", "P2", 1, false, null, null),
        };

        var state = new GameEngine().CreateMatchState(setup);

        Assert.Equal(0, state.InitiativePlayerIndex);
    }

    [Fact]
    public void ScheduleBuilder_CreatesTwentySevenMatchesPerPlayer()
    {
        var world = new WorldFactory().CreateNewWorld();
        var matches = world.Season.Schedule.Where(match => match.Phase == MatchPhase.RegularSeason).ToList();

        foreach (var player in world.Players.Values)
        {
            var count = matches.Count(match => match.HomePlayerId == player.Id || match.AwayPlayerId == player.Id);
            Assert.Equal(27, count);
        }
    }

    [Fact]
    public void PlayoffSeeds_IncludeDivisionWinnersAndWildCards()
    {
        var world = new WorldFactory().CreateNewWorld();
        var league = LeagueTier.Bronze;
        SeedStandingsForPlayoffs(world, league);

        var seeds = new WorldSimulator().GetPlayoffSeeds(world, league);

        Assert.Equal(8, seeds.Count);
        Assert.Equal(Enumerable.Range(1, 8), seeds.Select(seed => seed.Seed));
    }

    [Fact]
    public void PromotionRelegation_UsesFinalPlacements()
    {
        var world = new WorldFactory().CreateNewWorld();
        world.Season.FinalPlacements[LeagueTier.Bronze] = world.Players.Values.Where(player => player.League == LeagueTier.Bronze).OrderBy(player => player.Id).Select(player => player.Id).ToList();
        world.Season.FinalPlacements[LeagueTier.Silver] = world.Players.Values.Where(player => player.League == LeagueTier.Silver).OrderBy(player => player.Id).Select(player => player.Id).ToList();
        world.Season.FinalPlacements[LeagueTier.Gold] = world.Players.Values.Where(player => player.League == LeagueTier.Gold).OrderBy(player => player.Id).Select(player => player.Id).ToList();
        world.Season.FinalPlacements[LeagueTier.World] = world.Players.Values.Where(player => player.League == LeagueTier.World).OrderBy(player => player.Id).Select(player => player.Id).ToList();

        var moves = new WorldSimulator().GetPromotionRelegationMoves(world);

        Assert.Contains(moves, move => move.From == LeagueTier.Bronze && move.To == LeagueTier.Silver);
        Assert.Contains(moves, move => move.From == LeagueTier.World && move.To == LeagueTier.Gold);
        Assert.Equal(24, moves.Count);
    }

    [Fact]
    public void StandardRotation_MakesOldestSetIllegal()
    {
        var world = new WorldFactory().CreateNewWorld();
        var simulator = new WorldSimulator();
        var oldest = world.StandardSetIds.First();

        typeof(WorldSimulator).GetMethod("RotateStandard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(simulator, [world]);
        typeof(WorldSimulator).GetMethod("AddNewSet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(simulator, [world]);

        Assert.DoesNotContain(oldest, world.StandardSetIds);
        Assert.Equal(3, world.StandardSetIds.Count);
    }

    [Fact]
    public void CollectorPricing_UsesConfiguredBaseValues()
    {
        var world = new WorldFactory().CreateNewWorld();
        var commonUnit = world.CardVersions.Values.Select(list => list.Last().Definition).First(card => card.Rarity == CardRarity.Common && card.Type == CardType.Unit);
        var price = CollectorService.GetCollectorSellPrice(world, commonUnit.Id);
        var buyback = CollectorService.GetCollectorBuybackPrice(world, commonUnit.Id);

        Assert.InRange(price, 34, 55);
        Assert.InRange(buyback, 18, 31);
    }

    [Fact]
    public void PackOpening_UsesOnlyUnissuedLegendaryPool()
    {
        var world = new WorldFactory().CreateNewWorld();
        var player = world.Players.Values.Single(item => item.IsHuman);
        var newestSet = world.StandardSetIds.Last();
        var set = world.CardSets[newestSet];
        set.UnissuedLegendaryIds.Clear();

        var awarded = CollectorService.OpenPack(world, player, newestSet, 999);

        Assert.DoesNotContain(awarded, card => world.GetLatestDefinition(card.CardId).Rarity == CardRarity.Legendary);
    }

    [Fact]
    public void UnsoldRevealedLegendary_NeverReturnsToPackPool()
    {
        var world = new WorldFactory().CreateNewWorld();
        var newestSet = world.StandardSetIds.Last();
        var set = world.CardSets[newestSet];
        var legendaryId = set.UnissuedLegendaryIds.First();
        set.UnissuedLegendaryIds.Remove(legendaryId);
        world.CollectorInventory.Singles.Add(new CollectorSingleOffer
        {
            ListingId = "LEG-1",
            CardId = legendaryId,
            Version = world.GetLatestVersion(legendaryId).Version,
            Price = 500,
            IsLegendaryReveal = true,
        });

        CollectorService.Refresh(world, 5);

        Assert.DoesNotContain(legendaryId, set.UnissuedLegendaryIds);
        Assert.Contains(legendaryId, set.HiddenCollectorLegendaryIds);
    }

    [Fact]
    public void MarketSettlement_TransfersCardAndAppliesFee()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(item => item.IsHuman);
        var buyer = world.Players.Values.First(player => !player.IsHuman);
        var card = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var startingCash = seller.Cash;
        var listing = MarketService.CreateAuctionListing(world, seller.Id, card.InstanceId, 100)!;
        Assert.NotNull(listing);
        Assert.True(MarketService.PlaceBid(world, buyer.Id, listing.Id, 120));

        MarketService.SettleWeek(world);

        Assert.DoesNotContain(seller.Collection, owned => owned.InstanceId == card.InstanceId);
        Assert.Contains(buyer.Collection, owned => owned.InstanceId == card.InstanceId);
        Assert.Equal(startingCash + 114, seller.Cash);
    }

    [Fact]
    public void Patching_BumpsVersionAndCapsChangeCount()
    {
        var world = new WorldFactory().CreateNewWorld();
        var cardId = world.CardVersions.Keys.First();
        world.Season.CardStats[cardId] = new CardUsageStats
        {
            CardId = cardId,
            DeckAppearances = 10,
            MatchWins = 9,
            MatchLosses = 1,
            PlayoffDeckAppearances = 8,
            MarketDemand = 10,
            RoleDemand = 10,
        };

        typeof(WorldSimulator).GetMethod("ApplySeasonalPatches", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(new WorldSimulator(), [world]);

        Assert.True(world.CardVersions[cardId].Count >= 2);
        Assert.All(world.PatchHistory, patch => Assert.InRange(patch.Operations.Count, 1, 2));
    }

    private static void SeedStandingsForPlayoffs(World world, LeagueTier league)
    {
        foreach (var divisionId in world.Leagues[league].DivisionIds)
        {
            var players = world.Divisions[divisionId].PlayerIds;
            for (var index = 0; index < players.Count; index++)
            {
                world.Season.Standings[players[index]].MatchPoints = 40 - index;
                world.Season.Standings[players[index]].GameDifferential = 20 - index;
            }
        }
    }
}
