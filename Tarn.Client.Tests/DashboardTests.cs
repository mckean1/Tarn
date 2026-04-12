using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Screens.Dashboard;
using Tarn.ClientApp.Play.Rendering;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class DashboardTests
{
    [Fact]
    public void DashboardQueryBuildsRealSummary()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var model = new DashboardQueries().Build(world, human.Id);

        Assert.Equal(1, model.Year);
        Assert.Equal("Bronze", model.League);
        Assert.Contains("LEGAL", model.DeckLegality);
        Assert.NotNull(model.NextMatch);
        Assert.Equal("This Week", model.NextMatch!.Status);
        Assert.NotEmpty(model.RecommendedActions);
        Assert.Contains(model.RecentActivity, item => item.StartsWith("Collector •", StringComparison.Ordinal));
    }

    [Fact]
    public void DashboardRendererUsesBoxedSectionsAndSelectedAction()
    {
        var state = BuildState();
        state.Dashboard.SelectedActionIndex = 1;
        state.Dashboard.ViewModel = new DashboardViewModel(
            1,
            1,
            "Bronze",
            "0-0",
            1,
            100,
            "LEGAL",
            "31/31",
            new DashboardNextMatchViewModel("Manager 005", "Bronze", "This Week"),
            ["Match • W1 beat Manager 002 2-1", "Collector • refreshed for Week 1"],
            ["Open Schedule", "Advance Week", "Open Match Center"]);

        var output = DashboardRenderer.Render(state, new Rect(0, 0, 96, 18));
        var plainOutput = AnsiUtility.StripAnsi(output);
        var lines = plainOutput.Split(Environment.NewLine);

        Assert.Contains("Season Status", plainOutput);
        Assert.Contains("Next Match", plainOutput);
        Assert.Contains("Recent Activity", plainOutput);
        Assert.Contains("Recommended Actions", plainOutput);
        Assert.Contains("Deck: [LEGAL] 31/31", plainOutput);
        Assert.Contains("Opponent: Manager 005", plainOutput);
        Assert.Contains("> Advance Week", plainOutput);
        Assert.StartsWith("└", lines[4]);
        Assert.Contains("┌ Recent Activity", lines[5]);
        Assert.Contains("Collector • refreshed for Week 1", plainOutput);
        Assert.Equal(18, lines.Length);
        Assert.All(lines, line => Assert.Equal(96, line.Length));
        Assert.Equal('┐', lines[0][41]);
        Assert.Equal('┌', lines[0][44]);
        Assert.Equal('┘', lines[4][41]);
        Assert.Equal('└', lines[4][44]);
        Assert.Equal('│', lines[6][41]);
        Assert.Equal('│', lines[6][44]);
        Assert.Equal('┘', lines[^1][41]);
        Assert.Equal('┘', lines[^1][95]);

        if (TerminalStyle.SupportsAnsi)
        {
            Assert.Contains(TerminalStyle.BrightWhite, output);
        }
    }

    [Fact]
    public void DashboardQueryFormatsMultipleConciseRecentActivities()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        world.Season.CurrentWeek = 2;
        world.CollectorInventory.RefreshedWeek = 2;

        var match = world.Season.Schedule.First(item => item.Week == 1 && (item.HomePlayerId == human.Id || item.AwayPlayerId == human.Id));
        var opponentId = match.HomePlayerId == human.Id ? match.AwayPlayerId : match.HomePlayerId;
        match.Result = new MatchResult
        {
            WinnerPlayerId = human.Id,
            LoserPlayerId = opponentId,
            WinnerGameWins = 2,
            LoserGameWins = 1,
            Games =
            [
                new GameResult(1, 0, human.Id, opponentId),
                new GameResult(1, 0, opponentId, human.Id),
                new GameResult(1, 0, human.Id, opponentId),
            ],
        };

        var listedCard = human.Collection.First();
        world.MarketListings.Add(new MarketListing
        {
            Id = "AUC-001",
            Source = ListingSource.PlayerAuction,
            CardId = listedCard.CardId,
            Version = listedCard.Version,
            CardInstanceId = listedCard.InstanceId,
            SellerPlayerId = human.Id,
            MinimumBid = 8,
            CreatedWeek = 1,
            ExpiresWeek = 2,
            Status = ListingStatus.Sold,
            Bids = [new Bid { PlayerId = opponentId, Amount = 12 }],
        });

        var model = new DashboardQueries().Build(world, human.Id);

        Assert.Equal(3, model.RecentActivity.Count);
        Assert.Contains(model.RecentActivity, item => item.StartsWith("Match • W1 beat", StringComparison.Ordinal));
        Assert.Contains(model.RecentActivity, item => item == "Market • sold " + world.GetLatestDefinition(listedCard.CardId).Name);
        Assert.Contains(model.RecentActivity, item => item == "Collector • refreshed for Week 2");
    }

    [Fact]
    public void DashboardQueryUsesIntentionalEmptyRecentActivityState()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        world.CollectorInventory.RefreshedWeek = 0;

        var model = new DashboardQueries().Build(world, human.Id);

        Assert.Equal(["No recent activity yet. Advance the week or make a move."], model.RecentActivity);
    }

    [Fact]
    public void BoxDrawingRendersTitledPanelAndPadsLines()
    {
        var lines = BoxDrawing.RenderBox("Season Status", ["Record: 1-0"], 24, 4);

        Assert.Equal(4, lines.Count);
        Assert.StartsWith("┌ Season Status", lines[0]);
        Assert.StartsWith("│Record: 1-0", lines[1]);
        Assert.Equal(24, lines[1].Length);
        Assert.Equal(24, lines[2].Length);
        Assert.Equal("│", lines[2][..1]);
        Assert.Equal("│", lines[2][^1..]);
        Assert.StartsWith("└", lines[3]);
    }

    private static AppState BuildState()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        return new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
    }
}
