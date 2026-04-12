using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.MatchCenter;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class MatchCenterRendererTests
{
    [Fact]
    public void BattleSummaryShowsRoundInitiativeAndAutoplayState()
    {
        var state = BuildState(currentEventIndex: 6, autoplayEnabled: false);

        var output = AnsiUtility.StripAnsi(MatchCenterRenderer.Render(state, new Rect(0, 0, 120, 22)));

        Assert.Contains("Battle Summary", output);
        Assert.Contains("Fresh vs Manager 002", output);
        Assert.Contains("Round: 1", output);
        Assert.Contains("Initiative: Fresh", output);
        Assert.Contains("Autoplay: Off", output);
    }

    [Fact]
    public void BattlefieldShowsEmptyAndPopulatedBoardsWithCounters()
    {
        var state = BuildState(
            currentEventIndex: 6,
            homeBoardLines: ["[1] Ember Adept 2/3", "[2] Ash Guard 1/5 [Mag]"],
            awayBoardLines: ["empty"],
            homeCounters: "Brace the Line",
            awayCounters: "none");

        var output = AnsiUtility.StripAnsi(MatchCenterRenderer.Render(state, new Rect(0, 0, 120, 22)));

        Assert.Contains("Battlefield", output);
        Assert.Contains("Home board", output);
        Assert.Contains("[1] Ember Adept 2/3", output);
        Assert.Contains("[2] Ash Guard 1/5 [Mag]", output);
        Assert.Contains("Away board", output);
        Assert.Contains("empty", output);
        Assert.Contains("Home: Brace the Line", output);
        Assert.Contains("Away: none", output);
    }

    [Fact]
    public void EventLogShowsCompactSetupAndHighlightsCurrentReplayStep()
    {
        var state = BuildState(currentEventIndex: 7);

        var output = MatchCenterRenderer.Render(state, new Rect(0, 0, 120, 22));
        var plainOutput = AnsiUtility.StripAnsi(output);

        Assert.Contains("Replay Info", plainOutput);
        Assert.Contains("Decks: 12 vs 12 cards", plainOutput);
        Assert.Contains("Replay seed 1001.", plainOutput);
        Assert.Contains("Fresh deck ready.", plainOutput);
        Assert.Contains("> Null Unit 6 enters as 4/5.", plainOutput);
        Assert.DoesNotContain("UN001, UN002, UN003", plainOutput);
    }

    [Fact]
    public void CompletedReplayShowsClearResultPresentation()
    {
        var state = BuildState(currentEventIndex: 8, battleStateLabel: "Complete");

        var output = AnsiUtility.StripAnsi(MatchCenterRenderer.Render(state, new Rect(0, 0, 120, 22)));

        Assert.Contains("State: Complete", output);
        Assert.Contains("Result: Fresh wins 2-0", output);
    }

    private static AppState BuildState(
        int currentEventIndex,
        bool autoplayEnabled = false,
        string battleStateLabel = "In Progress",
        IReadOnlyList<string>? homeBoardLines = null,
        IReadOnlyList<string>? awayBoardLines = null,
        string homeCounters = "none",
        string awayCounters = "none")
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var replay = new MatchReplayViewModel(
            "MATCH-1",
            "Fresh vs Manager 002",
            "Fresh",
            "Fresh wins 2-0",
            ["Seed: 1001", "Home champion: Veyn", "Away champion: Serah", "Decks: 12 vs 12 cards"],
            [
                "Replay seed 1001.",
                "Fresh champion: Veyn.",
                "Manager 002 champion: Serah.",
                "Fresh deck ready.",
                "Manager 002 deck ready.",
                "Fresh has initiative.",
                "Round 1 begins.",
                "Null Unit 6 enters as 4/5.",
                "Win check: both champions survive."
            ],
            [
                new RoundSnapshotViewModel(
                    1,
                    "1",
                    battleStateLabel,
                    "Fresh",
                    new ChampionPanelViewModel("Home", "Fresh", "Veyn", 20, 0),
                    new ChampionPanelViewModel("Away", "Manager 002", "Serah", 18, 1),
                    homeBoardLines ?? ["empty"],
                    awayBoardLines ?? ["empty"],
                    homeCounters,
                    awayCounters,
                    8),
                new RoundSnapshotViewModel(
                    2,
                    "2",
                    "Complete",
                    "Manager 002",
                    new ChampionPanelViewModel("Home", "Fresh", "Veyn", 0, 1),
                    new ChampionPanelViewModel("Away", "Manager 002", "Serah", 4, 1),
                    homeBoardLines ?? ["empty"],
                    awayBoardLines ?? ["empty"],
                    homeCounters,
                    awayCounters,
                    9)
            ]);

        return new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
            ActiveScreen = ScreenId.MatchCenter,
            MatchCenter =
            {
                CurrentEventIndex = currentEventIndex,
                AutoplayEnabled = autoplayEnabled,
                Replay = replay,
            },
        };
    }
}
