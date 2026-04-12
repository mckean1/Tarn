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
    public void SeasonClose_PreservesPlayoffBasedFinalPlacements()
    {
        var world = new WorldFactory().CreateNewWorld();
        var playoffOrder = world.Players.Values.Where(player => player.League == LeagueTier.Bronze).OrderByDescending(player => player.Id, StringComparer.Ordinal).Select(player => player.Id).ToList();
        world.Season.FinalPlacements[LeagueTier.Bronze] = playoffOrder;
        SeedStandingsForPlayoffs(world, LeagueTier.Bronze);
        world.Season.CurrentWeek = world.Config.Season.SeasonCloseWeek;

        new WorldSimulator().ResolveAdministrativeWeek(world, 1);

        Assert.Equal(playoffOrder, world.Season.FinalPlacements[LeagueTier.Bronze]);
    }

    [Fact]
    public void Payouts_UsePlayoffBasedFinalPlacements()
    {
        var world = new WorldFactory().CreateNewWorld();
        var bronzePlayers = world.Players.Values.Where(player => player.League == LeagueTier.Bronze).OrderBy(player => player.Id, StringComparer.Ordinal).ToList();
        world.Season.FinalPlacements[LeagueTier.Bronze] = bronzePlayers.Select(player => player.Id).Reverse().ToList();
        var champion = world.Players[world.Season.FinalPlacements[LeagueTier.Bronze][0]];
        var runnerUp = world.Players[world.Season.FinalPlacements[LeagueTier.Bronze][1]];
        var beforeChampion = champion.Cash;
        var beforeRunnerUp = runnerUp.Cash;

        typeof(WorldSimulator).GetMethod("PayoutRewards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(new WorldSimulator(), [world]);

        Assert.Equal(beforeChampion + 2200, champion.Cash);
        Assert.Equal(beforeRunnerUp + 1800, runnerUp.Cash);
    }

    [Fact]
    public void PromotionRelegation_UsesPlayoffBasedFinalPlacements()
    {
        var world = new WorldFactory().CreateNewWorld();
        var bronzeOrder = world.Players.Values.Where(player => player.League == LeagueTier.Bronze).OrderByDescending(player => player.Id, StringComparer.Ordinal).Select(player => player.Id).ToList();
        world.Season.FinalPlacements[LeagueTier.Bronze] = bronzeOrder;

        var moves = new WorldSimulator().GetPromotionRelegationMoves(world);

        Assert.Equal(bronzeOrder.Take(4).ToList(), moves.Where(move => move.From == LeagueTier.Bronze).Select(move => move.PlayerId).ToList());
    }

    [Fact]
    public void SeasonClose_FallsBackToStandingsPlacementsWhenPlayoffPlacementsMissing()
    {
        var world = new WorldFactory().CreateNewWorld();
        SeedStandingsForPlayoffs(world, LeagueTier.Bronze);
        world.Season.CurrentWeek = world.Config.Season.SeasonCloseWeek;

        new WorldSimulator().ResolveAdministrativeWeek(world, 1);

        Assert.Equal(world.Config.Leagues.PlayersPerLeague, world.Season.FinalPlacements[LeagueTier.Bronze].Count);
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
    public void SellingLegendaryToCollector_PaysPlayerAndMovesCardToHiddenInventory()
    {
        var world = new WorldFactory().CreateNewWorld();
        var player = world.Players.Values.Single(item => item.IsHuman);
        var setId = world.StandardSetIds.Last();
        var legendaryId = world.CardSets[setId].UnissuedLegendaryIds.First();
        var owned = WorldFactory.GrantCard(world, player, legendaryId);
        var beforeCash = player.Cash;

        var sold = CollectorService.SellToCollector(world, player.Id, owned.InstanceId);

        Assert.True(sold);
        Assert.DoesNotContain(player.Collection, card => card.InstanceId == owned.InstanceId);
        Assert.Equal(beforeCash + CollectorService.GetCollectorBuybackPrice(world, legendaryId), player.Cash);
        Assert.Equal(LegendaryState.HiddenCollectorHeld, world.CollectorInventory.LegendaryStates[legendaryId]);
        Assert.Contains(legendaryId, world.CardSets[setId].HiddenCollectorLegendaryIds);
        Assert.DoesNotContain(legendaryId, world.CardSets[setId].UnissuedLegendaryIds);
    }

    [Fact]
    public void BuyingRevealedCollectorLegendary_SetsStateToOwned_AndDoesNotReturnToHiddenInventory()
    {
        var world = new WorldFactory().CreateNewWorld();
        var player = world.Players.Values.Single(item => item.IsHuman);
        var setId = world.StandardSetIds.Last();
        var legendaryId = world.CardSets[setId].UnissuedLegendaryIds.First();
        world.CollectorInventory.LegendaryStates[legendaryId] = LegendaryState.RevealedForCollector;
        world.CardSets[setId].UnissuedLegendaryIds.Remove(legendaryId);
        world.CardSets[setId].HiddenCollectorLegendaryIds.Remove(legendaryId);
        var listing = new CollectorSingleOffer
        {
            ListingId = "LEG-BUY-1",
            CardId = legendaryId,
            Version = world.GetLatestVersion(legendaryId).Version,
            Price = 500,
            IsLegendaryReveal = true,
        };
        world.CollectorInventory.Singles.Add(listing);

        var bought = CollectorService.BuySingle(world, player.Id, listing.ListingId);

        Assert.True(bought);
        Assert.Contains(player.Collection, card => string.Equals(card.CardId, legendaryId, StringComparison.Ordinal));
        Assert.DoesNotContain(world.CollectorInventory.Singles, offer => string.Equals(offer.ListingId, listing.ListingId, StringComparison.Ordinal));
        Assert.Equal(LegendaryState.Owned, world.CollectorInventory.LegendaryStates[legendaryId]);

        CollectorService.Refresh(world, 7);

        Assert.Equal(LegendaryState.Owned, world.CollectorInventory.LegendaryStates[legendaryId]);
        Assert.DoesNotContain(legendaryId, world.CardSets[setId].HiddenCollectorLegendaryIds);
    }

    [Fact]
    public void HiddenLegendaryChampion_CanRelistInChampionSlot()
    {
        var (world, targetId) = CreateHiddenLegendaryRelistWorld(CardType.Champion);
        var method = typeof(CollectorService).GetMethod("PickCollectorSingleCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var args = new object[] { world, world.StandardSetIds.Last(), CardType.Champion, new SeededRng(1), new HashSet<string>(StringComparer.Ordinal), false };

        var selected = (string?)method.Invoke(null, args);

        Assert.Equal(targetId, selected);
    }

    [Fact]
    public void HiddenNonChampionLegendary_CanRelistInMatchingNonChampionSlot()
    {
        var (world, targetId) = CreateHiddenLegendaryRelistWorld(CardType.Unit);
        var method = typeof(CollectorService).GetMethod("PickCollectorSingleCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var args = new object[] { world, world.StandardSetIds.Last(), CardType.Unit, new SeededRng(1), new HashSet<string>(StringComparer.Ordinal), false };

        var selected = (string?)method.Invoke(null, args);

        Assert.Equal(targetId, selected);
    }

    [Fact]
    public void ChampionSlots_NeverRelistNonChampionLegendary()
    {
        var (world, targetId) = CreateHiddenLegendaryRelistWorld(CardType.Unit);
        var method = typeof(CollectorService).GetMethod("PickCollectorSingleCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var args = new object[] { world, world.StandardSetIds.Last(), CardType.Champion, new SeededRng(1), new HashSet<string>(StringComparer.Ordinal), false };

        var selected = (string?)method.Invoke(null, args);

        Assert.NotEqual(targetId, selected);
    }

    [Fact]
    public void WinningBidReservesCash_AndOutbidReleasesIt()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var bidder = world.Players["PLY002"];
        bidder.Cash = 100;
        var outbidder = world.Players["PLY003"];
        var cardOne = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var listingOne = MarketService.CreateAuctionListing(world, seller.Id, cardOne.InstanceId, 40)!;

        Assert.True(MarketService.PlaceBid(world, bidder.Id, listingOne.Id, 60));
        Assert.Equal(40, MarketService.GetAvailableCashForBids(world, bidder.Id));

        Assert.True(MarketService.PlaceBid(world, outbidder.Id, listingOne.Id, 70));
        Assert.Equal(100, MarketService.GetAvailableCashForBids(world, bidder.Id));
    }

    [Fact]
    public void FreedCashCanBeUsedOnAnotherListingAfterOutbid()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var bidder = world.Players["PLY002"];
        bidder.Cash = 100;
        var outbidder = world.Players["PLY003"];
        var cardOne = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var cardTwo = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var listingOne = MarketService.CreateAuctionListing(world, seller.Id, cardOne.InstanceId, 60)!;
        var listingTwo = MarketService.CreateAuctionListing(world, seller.Id, cardTwo.InstanceId, 60)!;

        Assert.True(MarketService.PlaceBid(world, bidder.Id, listingOne.Id, 60));
        Assert.True(MarketService.PlaceBid(world, outbidder.Id, listingOne.Id, 70));
        Assert.True(MarketService.PlaceBid(world, bidder.Id, listingTwo.Id, 60));
    }

    [Fact]
    public void ReservationTotal_ReflectsOnlyCurrentWinningBids()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var bidder = world.Players["PLY002"];
        bidder.Cash = 200;
        var other = world.Players["PLY003"];
        var cardOne = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var cardTwo = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var listingOne = MarketService.CreateAuctionListing(world, seller.Id, cardOne.InstanceId, 60)!;
        var listingTwo = MarketService.CreateAuctionListing(world, seller.Id, cardTwo.InstanceId, 50)!;

        Assert.True(MarketService.PlaceBid(world, bidder.Id, listingOne.Id, 60));
        Assert.True(MarketService.PlaceBid(world, bidder.Id, listingTwo.Id, 50));
        Assert.True(MarketService.PlaceBid(world, other.Id, listingOne.Id, 70));

        Assert.Equal(150, MarketService.GetAvailableCashForBids(world, bidder.Id));
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
        world.Season.CurrentWeek = listing.ExpiresWeek;

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
        world.LastCompletedSeasonStats = new CompletedSeasonStats
        {
            SeasonYear = world.Season.Year,
            IsFrozen = true,
            CardStats = new Dictionary<string, CardUsageStats>
            {
                [cardId] = new()
                {
                    CardId = cardId,
                    DeckAppearances = 10,
                    MatchWins = 9,
                    MatchLosses = 1,
                    PlayoffDeckAppearances = 8,
                    MarketDemand = 10,
                    RoleDemand = 10,
                },
            },
        };

        typeof(WorldSimulator).GetMethod("ApplySeasonalPatches", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(new WorldSimulator(), [world]);

        Assert.True(world.CardVersions[cardId].Count >= 2);
        Assert.All(world.PatchHistory, patch => Assert.InRange(patch.Operations.Count, 1, 2));
    }

    [Fact]
    public void CompletedSeasonStats_SurviveIntoPatchWeek()
    {
        var world = new WorldFactory().CreateNewWorld();
        world.Season.CurrentWeek = world.Config.Season.SeasonCloseWeek;
        world.Season.CardStats["CARD-A"] = new CardUsageStats { CardId = "CARD-A", DeckAppearances = 3, MatchWins = 2, MatchLosses = 1 };
        var simulator = new WorldSimulator();

        simulator.ResolveAdministrativeWeek(world, seed: 1);
        Assert.Equal(3, world.LastCompletedSeasonStats!.CardStats["CARD-A"].DeckAppearances);

        typeof(WorldSimulator).GetMethod("StartNextSeasonSchedule", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(simulator, [world]);

        Assert.Equal(3, world.LastCompletedSeasonStats!.CardStats["CARD-A"].DeckAppearances);
        Assert.Empty(world.Season.CardStats);
    }

    [Fact]
    public void ApplySeasonalPatches_ReadsFrozenCompletedSeasonStats()
    {
        var world = new WorldFactory().CreateNewWorld();
        var cardId = world.CardVersions.Keys.First();
        world.LastCompletedSeasonStats = new CompletedSeasonStats
        {
            SeasonYear = 1,
            IsFrozen = true,
            CardStats = new Dictionary<string, CardUsageStats>
            {
                [cardId] = new()
                {
                    CardId = cardId,
                    DeckAppearances = 10,
                    MatchWins = 10,
                    MatchLosses = 0,
                    PlayoffDeckAppearances = 10,
                    MarketDemand = 10,
                    RoleDemand = 10,
                },
            },
        };
        world.Season.CardStats[cardId] = new CardUsageStats { CardId = cardId, DeckAppearances = 0, MatchWins = 0, MatchLosses = 10 };

        typeof(WorldSimulator).GetMethod("ApplySeasonalPatches", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(new WorldSimulator(), [world]);

        Assert.Contains(world.PatchHistory, patch => patch.CardId == cardId);
    }

    [Fact]
    public void NewSeasonStats_StartFreshAfterPatchingLifecycle()
    {
        var world = new WorldFactory().CreateNewWorld();
        world.Season.CurrentWeek = world.Config.Season.SeasonCloseWeek;
        world.Season.CardStats["CARD-A"] = new CardUsageStats { CardId = "CARD-A", DeckAppearances = 4 };
        var simulator = new WorldSimulator();

        simulator.ResolveAdministrativeWeek(world, 1);
        typeof(WorldSimulator).GetMethod("StartNextSeasonSchedule", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(simulator, [world]);
        world.Season.CurrentWeek = world.Config.Season.PatchWeek;
        simulator.ResolveAdministrativeWeek(world, 1);

        Assert.Empty(world.Season.CardStats);
        Assert.NotNull(world.LastCompletedSeasonStats);
    }

    [Fact]
    public void NewListing_RemainsOpenUntilNextWeeksSettlement()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var card = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);

        var listing = MarketService.CreateAuctionListing(world, seller.Id, card.InstanceId, 100)!;

        Assert.Equal(world.Season.CurrentWeek + 1, listing.ExpiresWeek);
        MarketService.SettleWeek(world);
        Assert.Equal(ListingStatus.Active, listing.Status);
    }

    [Fact]
    public void AiCanBidOnListingBeforeSettlement()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var card = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var listing = MarketService.CreateAuctionListing(world, seller.Id, card.InstanceId, 50)!;

        new WorldSimulator().StepWeek(world, 1);

        Assert.NotEmpty(listing.Bids);
    }

    [Fact]
    public void ListingsSettleOnNextWeeklyCadence()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var buyer = world.Players.Values.First(player => !player.IsHuman);
        var card = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var listing = MarketService.CreateAuctionListing(world, seller.Id, card.InstanceId, 50)!;
        Assert.True(MarketService.PlaceBid(world, buyer.Id, listing.Id, 60));

        var simulator = new WorldSimulator();
        simulator.StepWeek(world, 1);
        simulator.StepWeek(world, 2);

        Assert.Equal(ListingStatus.Sold, listing.Status);
    }

    [Fact]
    public void BiddersCannotOvercommitAcrossListings()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var buyer = world.Players.Values.First(player => !player.IsHuman);
        buyer.Cash = 100;
        var cardOne = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var cardTwo = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var listingOne = MarketService.CreateAuctionListing(world, seller.Id, cardOne.InstanceId, 60)!;
        var listingTwo = MarketService.CreateAuctionListing(world, seller.Id, cardTwo.InstanceId, 60)!;

        Assert.True(MarketService.PlaceBid(world, buyer.Id, listingOne.Id, 60));
        Assert.False(MarketService.PlaceBid(world, buyer.Id, listingTwo.Id, 60));
    }

    [Fact]
    public void SettlementFallsBackToNextValidBid()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var topBidder = world.Players["PLY002"];
        var fallbackBidder = world.Players["PLY003"];
        topBidder.Cash = 0;
        fallbackBidder.Cash = 200;
        var card = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var listing = MarketService.CreateAuctionListing(world, seller.Id, card.InstanceId, 50)!;
        listing.Bids.Add(new Bid { PlayerId = topBidder.Id, Amount = 100 });
        listing.Bids.Add(new Bid { PlayerId = fallbackBidder.Id, Amount = 80 });
        world.Season.CurrentWeek = listing.ExpiresWeek;

        MarketService.SettleWeek(world);

        Assert.Equal(ListingStatus.Sold, listing.Status);
        Assert.Contains(fallbackBidder.Collection, owned => owned.InstanceId == card.InstanceId);
        Assert.Equal(1576, seller.Cash);
    }

    [Fact]
    public void ListingExpiresOnlyWhenNoValidBidsRemain()
    {
        var world = new WorldFactory().CreateNewWorld();
        var seller = world.Players.Values.Single(player => player.IsHuman);
        var bidder = world.Players.Values.First(player => !player.IsHuman);
        bidder.Cash = 0;
        var card = WorldFactory.GrantCard(world, seller, seller.Collection.First(entry => world.GetLatestDefinition(entry.CardId).Type != CardType.Champion).CardId);
        var listing = MarketService.CreateAuctionListing(world, seller.Id, card.InstanceId, 50)!;
        listing.Bids.Add(new Bid { PlayerId = bidder.Id, Amount = 100 });
        world.Season.CurrentWeek = listing.ExpiresWeek;

        MarketService.SettleWeek(world);

        Assert.Equal(ListingStatus.Expired, listing.Status);
        Assert.Contains(seller.Collection, owned => owned.InstanceId == card.InstanceId);
    }

    [Fact]
    public void FirstRefreshAfterNewSetReleaseUsesLaunchBehavior()
    {
        var world = new WorldFactory().CreateNewWorld();
        world.Season.CurrentWeek = world.Config.Season.GrantWeek;
        world.NewestSetReleaseYear = world.Season.Year;
        world.NewestSetReleaseWeek = world.Season.CurrentWeek;

        CollectorService.Refresh(world, 1);

        Assert.Equal(CollectorPhase.Launch, CollectorService.GetPhase(world));
        Assert.Equal(world.Config.CollectorPhases[CollectorPhase.Launch].Singles, world.CollectorInventory.Singles.Count);
    }

    [Fact]
    public void NewestSetPacksUseLaunchPricingImmediatelyOnRelease()
    {
        var world = new WorldFactory().CreateNewWorld();
        world.Season.CurrentWeek = world.Config.Season.GrantWeek;
        world.NewestSetReleaseYear = world.Season.Year;
        world.NewestSetReleaseWeek = world.Season.CurrentWeek;

        var newestSetId = world.StandardSetIds.Last();

        Assert.Equal(world.Config.Economy.PackPrices.NewestLaunch, CollectorService.GetPackPrice(world, newestSetId));
    }

    [Fact]
    public void ReleaseWindowTransitionsFromLaunchToWarmToNormal()
    {
        var world = new WorldFactory().CreateNewWorld();
        world.NewestSetReleaseYear = world.Season.Year;
        world.NewestSetReleaseWeek = 10;

        world.Season.CurrentWeek = 10;
        Assert.Equal(CollectorPhase.Launch, CollectorService.GetPhase(world));
        world.Season.CurrentWeek = 15;
        Assert.Equal(CollectorPhase.Warm, CollectorService.GetPhase(world));
        world.Season.CurrentWeek = 19;
        Assert.Equal(CollectorPhase.Normal, CollectorService.GetPhase(world));
    }

    [Fact]
    public void CommonPackSlotsCanProduceCommonChampions()
    {
        var world = CreatePackTestWorld(CardRarity.Common);
        var player = world.Players.Values.Single(item => item.IsHuman);

        var awarded = CollectorService.OpenPack(world, player, world.StandardSetIds.Last(), 1);

        Assert.Contains(awarded, owned => world.GetLatestDefinition(owned.CardId).Type == CardType.Champion && world.GetLatestDefinition(owned.CardId).Rarity == CardRarity.Common);
    }

    [Fact]
    public void RarePackSlotsCanProduceRareChampions()
    {
        var world = CreatePackTestWorld(CardRarity.Rare);
        var player = world.Players.Values.Single(item => item.IsHuman);

        var awarded = CollectorService.OpenPack(world, player, world.StandardSetIds.Last(), 1);

        Assert.Contains(awarded, owned => world.GetLatestDefinition(owned.CardId).Type == CardType.Champion && world.GetLatestDefinition(owned.CardId).Rarity == CardRarity.Rare);
    }

    [Fact]
    public void EpicNonUpgradedSlotsCanProduceEpicChampions()
    {
        var world = CreatePackTestWorld(CardRarity.Epic);
        var player = world.Players.Values.Single(item => item.IsHuman);

        var awarded = CollectorService.OpenPack(world, player, world.StandardSetIds.Last(), 1);

        Assert.Contains(awarded, owned => world.GetLatestDefinition(owned.CardId).Type == CardType.Champion && world.GetLatestDefinition(owned.CardId).Rarity == CardRarity.Epic);
    }

    [Fact]
    public void LegendaryUpgradesStillRespectUnissuedOnlyRules()
    {
        var world = new WorldFactory().CreateNewWorld();
        var player = world.Players.Values.Single(item => item.IsHuman);
        var newestSet = world.StandardSetIds.Last();
        var set = world.CardSets[newestSet];
        var issued = set.UnissuedLegendaryIds.First();
        set.HiddenCollectorLegendaryIds.Add(issued);
        set.UnissuedLegendaryIds.Remove(issued);

        var awarded = CollectorService.OpenPack(world, player, newestSet, 999);

        Assert.DoesNotContain(awarded, owned => string.Equals(owned.CardId, issued, StringComparison.Ordinal));
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

    private static World CreatePackTestWorld(CardRarity highlightedRarity)
    {
        var world = new WorldFactory().CreateNewWorld();
        var setId = world.StandardSetIds.Last();
        var cards = world.CardVersions.Values.Select(list => list.Last().Definition).Where(card => string.Equals(card.SetId, setId, StringComparison.Ordinal)).ToList();
        var commonChampion = cards.First(card => card.Type == CardType.Champion && card.Rarity == CardRarity.Common).Id;
        var commonOther = cards.First(card => card.Type != CardType.Champion && card.Rarity == CardRarity.Common).Id;
        var rareChampion = cards.First(card => card.Type == CardType.Champion && card.Rarity == CardRarity.Rare).Id;
        var rareOther = cards.First(card => card.Type != CardType.Champion && card.Rarity == CardRarity.Rare).Id;
        var epicChampion = cards.First(card => card.Type == CardType.Champion && card.Rarity == CardRarity.Epic).Id;
        var epicOther = cards.First(card => card.Type != CardType.Champion && card.Rarity == CardRarity.Epic).Id;
        var legendary = cards.First(card => card.Rarity == CardRarity.Legendary).Id;

        var chosen = new List<string> { legendary };
        chosen.Add(highlightedRarity == CardRarity.Common ? commonChampion : commonOther);
        chosen.Add(highlightedRarity == CardRarity.Rare ? rareChampion : rareOther);
        chosen.Add(highlightedRarity == CardRarity.Epic ? epicChampion : epicOther);

        world.CardSets[setId] = new CardSet
        {
            Id = setId,
            Sequence = world.CardSets[setId].Sequence,
            Name = world.CardSets[setId].Name,
            Keywords = world.CardSets[setId].Keywords,
            CardIds = chosen,
            UnissuedLegendaryIds = new HashSet<string>([legendary], StringComparer.Ordinal),
            HiddenCollectorLegendaryIds = new HashSet<string>(StringComparer.Ordinal),
        };

        return world;
    }

    private static (World World, string TargetId) CreateHiddenLegendaryRelistWorld(CardType family)
    {
        var world = new WorldFactory().CreateNewWorld();
        var setId = world.StandardSetIds.Last();
        var target = world.CardVersions.Values
            .Select(list => list.Last().Definition)
            .First(card => string.Equals(card.SetId, setId, StringComparison.Ordinal) && card.Rarity == CardRarity.Legendary && card.Type == family);
        world.CardSets[setId].HiddenCollectorLegendaryIds.Clear();
        world.CardSets[setId].HiddenCollectorLegendaryIds.Add(target.Id);
        world.CardSets[setId].UnissuedLegendaryIds.Remove(target.Id);
        world.CollectorInventory.LegendaryStates[target.Id] = LegendaryState.HiddenCollectorHeld;
        return (world, target.Id);
    }
}
