using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class PlayScreenSupportTests
{
    [Fact]
    public void SelectionMoveClampsWithinBounds()
    {
        Assert.Equal(0, ScreenSelection.Move(0, 3, -1));
        Assert.Equal(2, ScreenSelection.Move(2, 3, 1));
        Assert.Equal(0, ScreenSelection.Move(5, 0, 1));
    }

    [Fact]
    public void SelectionCycleWrapsAround()
    {
        var values = new[] { "A", "B", "C" };

        Assert.Equal("C", ScreenSelection.Cycle("A", values, -1));
        Assert.Equal("A", ScreenSelection.Cycle("C", values, 1));
    }

    [Fact]
    public void ScreenCatalogMapsShortcutAndNavigationLabels()
    {
        Assert.True(PlayScreenCatalog.TryGetShortcut(InputAction.Screen6, out var screen));
        Assert.Equal(ScreenId.Collection, screen.Id);
        Assert.Contains("8 Collector", PlayScreenCatalog.BuildGlobalNavigationText(compact: false, prefix: string.Empty));
    }

    [Fact]
    public void ReplayNavigationResetsReplayStateAndTracksReturnScreen()
    {
        var state = BuildState();
        state.ActiveScreen = ScreenId.Schedule;
        state.MatchCenter.CurrentEventIndex = 9;

        var result = ReplayNavigation.OpenReplay(state, ScreenId.Schedule, "MATCH-1");

        Assert.Equal("MATCH-1", state.MatchCenter.MatchId);
        Assert.Equal(0, state.MatchCenter.CurrentEventIndex);
        Assert.Equal(ScreenId.Schedule, state.MatchCenter.ReturnScreen);
        Assert.Equal(ScreenId.MatchCenter, result.NavigateTo);
        Assert.True(result.RequiresRefresh);
    }

    [Fact]
    public void ScreenTextFitLinesTruncatesAndClampsHeight()
    {
        var output = ScreenText.FitLines(["AlphaBeta", "Gamma", "Delta"], 6, 2);
        var lines = output.Split(Environment.NewLine);

        Assert.Equal(2, lines.Length);
        Assert.Equal("Alpha.", lines[0]);
        Assert.Equal("Gamma ", lines[1]);
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
