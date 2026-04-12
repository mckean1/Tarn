using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Screens.Collector;
using Tarn.ClientApp.Play.Screens.Market;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class CollectorMarketTests
{
    [Theory]
    [InlineData(100, 80, "Affordable")]
    [InlineData(50, 80, "Too Expensive")]
    public void FormatsAffordabilityLabels(int cash, int price, string expected)
    {
        Assert.Equal(expected, CollectorQueries.FormatAffordability(cash, price));
    }

    [Fact]
    public void CollectorTabsSwitchCleanly()
    {
        var state = BuildState();
        var controller = new CollectorController();

        controller.Handle(state, InputAction.MoveRight);

        Assert.Equal(CollectorTab.Packs, state.Collector.Tab);
    }

    [Fact]
    public void FormatsPackReveal()
    {
        var text = CollectorQueries.FormatPackReveal(
        [
            new PackRevealCard("Ash Drifter", "Common", true),
            new PackRevealCard("Snap Denial", "Rare", false),
        ]);

        Assert.Contains("1 new, 1 dupes", text);
        Assert.Contains("[Common] Ash Drifter NEW", text);
        Assert.Contains("[Rare] Snap Denial DUPE", text);
    }

    [Fact]
    public void FormatsTimeLeftAndListingStatus()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var listing = new MarketListing
        {
            Id = "A",
            Source = ListingSource.PlayerAuction,
            CardId = world.StandardSetIds.SelectMany(id => world.CardSets[id].CardIds).First(),
            Version = 1,
            CardInstanceId = "OWN1",
            SellerPlayerId = world.Players.Values.First().Id,
            MinimumBid = 10,
            CreatedWeek = world.Season.CurrentWeek,
            ExpiresWeek = world.Season.CurrentWeek + 1,
            Status = ListingStatus.Active,
        };

        Assert.Equal("1w left", MarketQueries.FormatTimeLeft(world, listing));
        Assert.Equal("Active", MarketQueries.FormatStatus(listing));
    }

    [Fact]
    public void MarketListingCreationStateAdjustsPrice()
    {
        var state = BuildState();
        state.Market.Tab = MarketTab.CreateListing;
        state.Market.ProposedBidOrPrice = 5;
        var controller = new MarketController();

        controller.Handle(state, InputAction.NextRound);

        Assert.Equal(6, state.Market.ProposedBidOrPrice);
    }

    [Fact]
    public void BuyingRevealedLegendaryKeepsItOwnedAfterRefresh()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var world = new WorldFactory().CreateNewWorld(1, "You");
            var player = world.Players.Values.Single(p => p.IsHuman);
            var legendaryId = world.CardSets[world.StandardSetIds[0]].CardIds
                .Select(world.GetLatestDefinition)
                .First(card => card.Rarity == CardRarity.Legendary)
                .Id;
            world.CollectorInventory.Singles.Clear();
            world.CollectorInventory.Singles.Add(new CollectorSingleOffer
            {
                ListingId = "LEG-1",
                CardId = legendaryId,
                Version = world.GetLatestVersion(legendaryId).Version,
                Price = 1,
                IsLegendaryReveal = true,
            });
            world.CollectorInventory.LegendaryStates[legendaryId] = LegendaryState.RevealedForCollector;
            WorldStorage.Save(world, tempPath);

            var refresh = new RefreshService();
            var state = refresh.CreateInitialState(tempPath, world);
            state.Modal = new ModalState
            {
                Kind = ModalKind.Confirmation,
                Title = "Buy",
                Lines = ["Buy"],
                PendingAction = new PendingAction(PendingActionKind.BuyCollectorSingle, "Buy", "Buy", ReferenceId: "LEG-1"),
            };
            new ActionExecutor(refresh).ExecutePending(state);
            CollectorService.Refresh(state.World, 7);

            Assert.Equal(LegendaryState.Owned, state.World.CollectorInventory.LegendaryStates[legendaryId]);
            Assert.DoesNotContain(legendaryId, state.World.CardSets[state.World.GetLatestDefinition(legendaryId).SetId].HiddenCollectorLegendaryIds);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static AppState BuildState()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        return new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = Path.GetTempFileName(),
        };
    }
}
