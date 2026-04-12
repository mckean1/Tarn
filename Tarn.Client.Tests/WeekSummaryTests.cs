using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.Dashboard;
using Tarn.ClientApp.Play.Screens.WeekSummary;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class WeekSummaryTests
{
    [Theory]
    [InlineData(3, "+3")]
    [InlineData(0, "0")]
    [InlineData(-2, "-2")]
    public void FormatsSignedDelta(int value, string expected)
    {
        Assert.Equal(expected, WeekSummaryQueries.FormatSignedDelta(value));
    }

    [Fact]
    public void DashboardAdvanceWeekProducesConfirmationModal()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var state = new RefreshService().CreateInitialState("test.json", world);
        var controller = new DashboardController();

        var result = controller.Handle(state, InputAction.AdvanceWeek);

        Assert.NotNull(result.Modal);
        Assert.Equal("Advance Week?", result.Modal!.Title);
        Assert.Equal(PendingActionKind.AdvanceWeek, result.Modal.PendingAction!.Kind);
    }

    [Fact]
    public void EmptyWeekSummaryRendersIntentionalSummaryCard()
    {
        var state = BuildState();
        state.WeekSummary.Summary = new WeekSummaryQueries().BuildDefault();

        var output = AnsiUtility.StripAnsi(WeekSummaryRenderer.Render(state, new Rect(0, 0, 80, 16)));

        Assert.Contains("No Summary Available", output);
        Assert.Contains("Advance the week from Dashboard", output);
        Assert.Contains("Actions", output);
        Assert.Contains("Return to Dashboard", output);
        Assert.DoesNotContain("Open Replay", output);
    }

    [Fact]
    public void CompletedWeekSummaryShowsProgressionAndReplayAction()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var previousStanding = world.Season.Standings[human.Id];
        var previousRank = StandingsCalculator.Rank(world.Season.Standings.Values.Where(entry => entry.League == human.League).ToList())
            .First(entry => entry.DeckId == human.Id)
            .LeagueRank;
        var previousCash = human.Cash;

        new WorldSimulator().StepWeek(world, 1001);

        var summary = new WeekSummaryQueries().BuildAfterAdvance(world, human.Id, 1, 1, previousCash, previousStanding.Wins, previousStanding.Losses, previousRank);
        var state = new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
        state.WeekSummary.Summary = summary;

        var output = AnsiUtility.StripAnsi(WeekSummaryRenderer.Render(state, new Rect(0, 0, 90, 16)));

        Assert.True(summary.IsGenerated);
        Assert.NotNull(summary.ReplayMatchId);
        Assert.Contains("Week 1 Complete", output);
        Assert.Contains("Result:", output);
        Assert.Contains("Record:", output);
        Assert.Contains("Rank:", output);
        Assert.Contains("Cash:", output);
        Assert.Contains("Collector refreshed", output);
        Assert.Contains("Open Replay", output);
        Assert.Contains("Return to Dashboard", output);
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
