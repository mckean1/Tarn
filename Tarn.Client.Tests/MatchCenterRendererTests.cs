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
        Assert.Contains("Round 1", output);
        Assert.Contains("Fresh initiative", output);
        Assert.Contains("Step: 7/9", output);
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
        Assert.Contains("Home Board", output);
        Assert.Contains("[1] Ember Adept 2/3", output);
        Assert.Contains("[2] Ash Guard 1/5 [Mag]", output);
        Assert.Contains("Away Board: empty", output);
        Assert.Contains("Counters", output);
        Assert.Contains("Home: Brace the Line", output);
        Assert.Contains("Away: none", output);
    }

    [Fact]
    public void EventLogShowsCompactSetupAndHighlightsCurrentReplayStep()
    {
        var state = BuildState(currentEventIndex: 7);

        var output = MatchCenterRenderer.Render(state, new Rect(0, 0, 120, 22));
        var plainOutput = AnsiUtility.StripAnsi(output);

        Assert.Contains("Replay seed 1001.", plainOutput);
        Assert.Contains("Both champions are ready.", plainOutput);
        Assert.DoesNotContain("Deck ready", plainOutput);
        Assert.Contains("▶ Null Unit 6 enters as 4/5.", plainOutput);
        Assert.DoesNotContain("UN001, UN002, UN003", plainOutput);
        Assert.DoesNotContain("Champions: Veyn vs Serah", plainOutput);
    }

    [Fact]
    public void OpeningStateShowsReplayMetadataAsSecondarySupport()
    {
        var state = BuildState(currentEventIndex: 1, battleStateLabel: "Opening", roundLabel: "Setup", roundNumber: 0);

        var output = AnsiUtility.StripAnsi(MatchCenterRenderer.Render(state, new Rect(0, 0, 120, 22)));

        Assert.Contains("Replay", output);
        Assert.Contains("Seed 1001", output);
        Assert.Contains("Champions: Veyn vs Serah", output);
        Assert.Contains("Decks: 12 vs 12", output);
    }

    [Fact]
    public void CompletedReplayShowsClearResultPresentation()
    {
        var state = BuildState(currentEventIndex: 8, battleStateLabel: "Complete");

        var output = AnsiUtility.StripAnsi(MatchCenterRenderer.Render(state, new Rect(0, 0, 120, 22)));

        Assert.Contains("Complete", output);
        Assert.Contains("Result: Fresh wins 2-0", output);
    }

    [Fact]
    public void CurrentReplayLineUsesSharedSelectedRowTreatment()
    {
        var state = BuildState(currentEventIndex: 7);

        var output = AnsiUtility.StripAnsi(MatchCenterRenderer.Render(state, new Rect(0, 0, 120, 22)));

        Assert.Contains("▶ Null Unit 6 enters as 4/5.", output);
        Assert.Contains("  Fresh has initiative.", output);
        Assert.Contains("  Win check: both champions survive.", output);
    }

    private static AppState BuildState(
        int currentEventIndex,
        bool autoplayEnabled = false,
        string battleStateLabel = "In Progress",
        int roundNumber = 1,
        string roundLabel = "1",
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
            ["Seed 1001", "Champions: Veyn vs Serah", "Decks: 12 vs 12"],
            [
                "Replay seed 1001.",
                "Both champions are ready.",
                "Both champions are ready.",
                string.Empty,
                string.Empty,
                "Fresh has initiative.",
                "Round 1 begins.",
                "Null Unit 6 enters as 4/5.",
                "Win check: both champions survive."
            ],
            [
                new RoundSnapshotViewModel(
                    roundNumber,
                    roundLabel,
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
