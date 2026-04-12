using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.Schedule;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class ScheduleTests
{
    [Fact]
    public void ScheduleQueryClampsSelection()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var model = new ScheduleQueries().Build(world, 1, 999, human.Id);

        Assert.True(model.SelectedFixtureIndex < model.Fixtures.Count);
        Assert.All(model.Fixtures, fixture => Assert.Equal(human.League.ToString(), fixture.LeagueLabel));
    }

    [Fact]
    public void ScheduleQueryFocusesCurrentLeagueAndOwnFixtureFirst()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var model = new ScheduleQueries().Build(world, 1, 0, human.Id);

        Assert.Equal(human.League.ToString(), model.FocusLeagueLabel);
        Assert.NotEmpty(model.Fixtures);
        Assert.All(model.Fixtures, fixture => Assert.Equal(human.League.ToString(), fixture.LeagueLabel));
        Assert.True(model.Fixtures[0].IsPlayerFixture);
        Assert.Contains("You", model.Fixtures[0].Pairing);
    }

    [Fact]
    public void ScheduleQueryFormatsRowsWithReadableStatusAndResult()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var match = world.Season.Schedule.First(item => item.Week == 1 && item.League == human.League && (item.HomePlayerId == human.Id || item.AwayPlayerId == human.Id));
        match.Result = new MatchResult
        {
            WinnerPlayerId = match.HomePlayerId,
            LoserPlayerId = match.AwayPlayerId,
            WinnerGameWins = 2,
            LoserGameWins = 1,
            Games = [],
        };

        var model = new ScheduleQueries().Build(world, 1, 0, human.Id);
        var fixture = model.Fixtures.First(item => item.MatchId == match.Id);

        Assert.Equal("Complete", fixture.Status);
        Assert.Equal("2-1", fixture.Result);
        Assert.DoesNotContain("[Pending]", fixture.Status);
    }

    [Fact]
    public void ScheduleDetailShowsPendingFixtureSummary()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var model = new ScheduleQueries().Build(world, 1, 0, human.Id);

        Assert.Contains("Match:", model.Detail.Lines[0]);
        Assert.Contains("Week:", model.Detail.Lines[1]);
        Assert.Contains("League:", model.Detail.Lines[2]);
        Assert.Contains("Status: Pending", model.Detail.Lines);
        Assert.Contains("Replay: Not available", model.Detail.Lines);
        Assert.Contains("Focus: Your match", model.Detail.Lines);
    }

    [Fact]
    public void ScheduleDetailShowsCompletedFixtureReplayAvailability()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var match = world.Season.Schedule.First(item => item.Week == 1 && item.League == human.League && (item.HomePlayerId == human.Id || item.AwayPlayerId == human.Id));
        match.Result = new MatchResult
        {
            WinnerPlayerId = match.HomePlayerId,
            LoserPlayerId = match.AwayPlayerId,
            WinnerGameWins = 2,
            LoserGameWins = 0,
            Games = [],
        };

        var detail = new ScheduleQueries().BuildDetail(world, match.Id, human.Id);

        Assert.True(detail.ReplayAvailable);
        Assert.Contains("Status: Complete", detail.Lines);
        Assert.Contains("Replay: Available", detail.Lines);
        Assert.Contains("Result:", detail.Lines.Single(line => line.StartsWith("Result:", StringComparison.Ordinal)));
    }

    [Fact]
    public void ScheduleControllerChangesWeekAndRefreshes()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var state = new RefreshService().CreateInitialState("test.json", world);
        var controller = new ScheduleController();

        state.Schedule.SelectedWeek = 1;
        var result = controller.Handle(state, InputAction.MoveLeft);

        Assert.True(result.RequiresRefresh);
        Assert.Equal(1, state.Schedule.SelectedWeek);
    }

    [Fact]
    public void ScheduleSelectionRefreshesDetailImmediately()
    {
        var state = new RefreshService().CreateInitialState("test.json", new WorldFactory().CreateNewWorld(1, "You"));
        var controller = new ScheduleController();
        var originalMatchId = state.Schedule.Detail!.MatchId;

        controller.Handle(state, InputAction.MoveDown);

        Assert.Equal(1, state.Schedule.SelectedFixtureIndex);
        Assert.Equal(state.Schedule.Fixtures[1].MatchId, state.Schedule.Detail!.MatchId);
        Assert.NotEqual(originalMatchId, state.Schedule.Detail.MatchId);
    }

    [Fact]
    public void ScheduleSelectionClampsAndKeepsDetailInSync()
    {
        var state = new RefreshService().CreateInitialState("test.json", new WorldFactory().CreateNewWorld(1, "You"));
        var controller = new ScheduleController();

        controller.Handle(state, InputAction.MoveUp);
        Assert.Equal(0, state.Schedule.SelectedFixtureIndex);
        Assert.Equal(state.Schedule.Fixtures[0].MatchId, state.Schedule.Detail!.MatchId);

        state.Schedule.SelectedFixtureIndex = state.Schedule.Fixtures.Count - 1;
        state.Schedule.Detail = new ScheduleQueries().BuildSelectionDetail(state.World, state.Schedule.SelectedWeek, state.Schedule.Fixtures, state.Schedule.SelectedFixtureIndex, state.HumanPlayerId);

        controller.Handle(state, InputAction.MoveDown);

        Assert.Equal(state.Schedule.Fixtures.Count - 1, state.Schedule.SelectedFixtureIndex);
        Assert.Equal(state.Schedule.Fixtures[^1].MatchId, state.Schedule.Detail!.MatchId);
    }

    [Fact]
    public void ScheduleWeekChangesRefreshRowsAndDetail()
    {
        var refresh = new RefreshService();
        var state = refresh.CreateInitialState("test.json", new WorldFactory().CreateNewWorld(1, "You"));
        var controller = new ScheduleController();
        var previousMatchId = state.Schedule.Detail!.MatchId;

        state.Schedule.SelectedWeek = 1;
        var result = controller.Handle(state, InputAction.MoveRight);
        if (result.RequiresRefresh)
        {
            refresh.RefreshSchedule(state);
        }

        Assert.Equal(2, state.Schedule.SelectedWeek);
        Assert.All(state.Schedule.Fixtures.Select(item => state.World.Season.Schedule.First(match => match.Id == item.MatchId)), match =>
        {
            Assert.Equal(2, match.Week);
            Assert.Equal(state.HumanPlayer.League, match.League);
        });
        Assert.Equal(state.Schedule.Fixtures[state.Schedule.SelectedFixtureIndex].MatchId, state.Schedule.Detail!.MatchId);
        Assert.NotEqual(previousMatchId, state.Schedule.Detail.MatchId);
    }

    [Fact]
    public void ScheduleRendererBuildsStructuredTableAndDetailPane()
    {
        var refresh = new RefreshService();
        var state = refresh.CreateInitialState("test.json", new WorldFactory().CreateNewWorld(1, "You"));
        state.ActiveScreen = ScreenId.Schedule;

        var output = ScheduleRenderer.Render(state, new Rect(0, 0, 90, 14));
        var plainOutput = AnsiUtility.StripAnsi(output);

        Assert.Contains("Fixtures · Week 1", plainOutput);
        Assert.Contains("Selected Fixture", plainOutput);
        Assert.Contains("Pairing", plainOutput);
        Assert.Contains("State", plainOutput);
        Assert.Contains("Result", plainOutput);
        Assert.Contains("> You vs", plainOutput);
        Assert.Contains("Replay:", plainOutput);
    }
}
