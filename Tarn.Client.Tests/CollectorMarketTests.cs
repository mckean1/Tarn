using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.Collector;
using Tarn.ClientApp.Play.Screens.Market;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class CollectorMarketTests
{
    [Theory]
    [InlineData(100, 80, "Affordable")]
    [InlineData(50, 80, "Not enough cash")]
    public void FormatsAffordabilityLabels(int cash, int price, string expected)
    {
        Assert.Equal(expected, CollectorQueries.FormatAffordability(cash, price));
    }

    [Fact]
    public void CollectorRendererFormatsSinglesWithDetailPane()
    {
        var state = BuildState();
        state.Collector.Tab = CollectorTab.Singles;
        state.Collector.SelectedIndex = 0;
        var row = new CollectorRowViewModel(
            "S-1",
            "Ashen Scout",
            "Unit",
            "Common",
            38,
            "Affordable",
            "Collector single",
            "Unit. No keyword.",
            "ATK 1 · HP 4 · SPD 0",
            "None",
            "Buy for 38",
            "Buy for 38",
            "Owned: 3");
        state.Collector.ViewModel = new CollectorViewModel(CollectorTab.Singles, 0, [row], row);

        var output = CollectorRenderer.Render(state, new Rect(0, 0, 100, 18));
        var plainOutput = AnsiUtility.StripAnsi(output);

        Assert.Contains("[Singles]  Packs  Sell", plainOutput);
        Assert.Contains("Name", plainOutput);
        Assert.Contains("Type", plainOutput);
        Assert.Contains("Rarity", plainOutput);
        Assert.Contains("Price", plainOutput);
        Assert.Contains("Status", plainOutput);
        Assert.Contains("> Ashen Scout", plainOutput);
        Assert.Contains("┌ Selected Item", plainOutput);
        Assert.Contains("Keywords: None", plainOutput);
        Assert.Contains("Buy for 38", plainOutput);
        Assert.DoesNotContain("Left/Right tabs, Up/Down selection", plainOutput);

        if (TerminalStyle.SupportsAnsi)
        {
            Assert.Contains(TerminalStyle.BrightWhite, output);
        }
    }

    [Fact]
    public void CollectorRendererMakesPacksFeelDistinct()
    {
        var state = BuildState();
        state.Collector.Tab = CollectorTab.Packs;
        state.Collector.SelectedIndex = 0;
        var row = new CollectorRowViewModel(
            "P-1",
            "Starter Pack",
            "Pack",
            string.Empty,
            100,
            "Affordable",
            "Contains 10 cards · Common-heavy",
            "Contains 5 Commons, 3 Rares, 2 Epics, with a chance to upgrade an Epic slot into a Legendary.",
            null,
            "N/A",
            "Open for 100",
            "Open for 100",
            null);
        state.Collector.ViewModel = new CollectorViewModel(CollectorTab.Packs, 0, [row], row);

        var plainOutput = AnsiUtility.StripAnsi(CollectorRenderer.Render(state, new Rect(0, 0, 100, 18)));

        Assert.Contains("Pack", plainOutput);
        Assert.Contains("Contents", plainOutput);
        Assert.Contains("Starter Pack", plainOutput);
        Assert.Contains("Common-heavy", plainOutput);
        Assert.Contains("┌ Selected Pack", plainOutput);
        Assert.Contains("Open for 100", plainOutput);
    }

    [Fact]
    public void CollectorRendererShowsSellColumnsAndEmptyStates()
    {
        var state = BuildState();
        state.Collector.Tab = CollectorTab.Sell;
        state.Collector.SelectedIndex = 0;
        var row = new CollectorRowViewModel(
            "SELL-1",
            "Briar Duelist",
            "Unit",
            "Rare",
            12,
            "Sellable",
            "Owned: 3",
            "Unit. Defender",
            "ATK 2 · HP 5 · SPD 1",
            "Defender",
            "Sell for 12",
            "Sell for 12",
            "Owned: 3",
            3);
        state.Collector.ViewModel = new CollectorViewModel(CollectorTab.Sell, 0, [row], row);

        var plainOutput = AnsiUtility.StripAnsi(CollectorRenderer.Render(state, new Rect(0, 0, 100, 18)));

        Assert.Contains("Owned", plainOutput);
        Assert.Contains("Sell", plainOutput);
        Assert.Contains("Sellable", plainOutput);
        Assert.Contains("Briar Duelist x3", plainOutput);
        Assert.Contains("Sell for 12", plainOutput);

        state.Collector.ViewModel = new CollectorViewModel(CollectorTab.Sell, 0, [], null);
        plainOutput = AnsiUtility.StripAnsi(CollectorRenderer.Render(state, new Rect(0, 0, 100, 14)));
        Assert.Contains("[Nothing to Sell]", plainOutput);
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
            new PackRevealCard("UN001", "Ashen Scout", CardType.Unit, CardRarity.Common, true),
            new PackRevealCard("UN001", "Ashen Scout", CardType.Unit, CardRarity.Common, false),
            new PackRevealCard("CT001", "Bastion Ward", CardType.Counter, CardRarity.Rare, false),
        ]);

        Assert.Contains("1 new, 2 dupes", text);
        Assert.Contains("[Common] Ashen Scout x2 1 new", text);
        Assert.Contains("[Rare] Bastion Ward x1 DUPE", text);
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
