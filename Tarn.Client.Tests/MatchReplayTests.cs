using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Screens.MatchCenter;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class MatchReplayTests
{
    [Theory]
    [InlineData("   1 | P1 Champion: Veyn", "Home champion: Veyn.")]
    [InlineData("   2 | P2 plays UN014 Hearthblade Initiate.", "Away plays Hearthblade Initiate.")]
    [InlineData("   3 | P1 Deck: UN001, UN002, UN003", "Home deck ready.")]
    [InlineData("   4 | P1-UN014-1 enters the Battlefield as 4/5.", "UN014 enters as 4/5.")]
    [InlineData("   5 | Win check: P1=20, P2=18.", "Win check: both champions survive.")]
    [InlineData("  20 | Enter Overtime", "Both champions fall. Overtime begins.")]
    [InlineData("  21 | P1 is out of cards and takes Fatigue 1.", "Home is out of cards and takes Fatigue 1.")]
    public void PhrasesReplayEvents(string raw, string expected)
    {
        Assert.Equal(expected, MatchReplayQueries.PhraseEvent(raw));
    }

    [Fact]
    public void AutoplayAdvancesReplayStateOverTicks()
    {
        var state = BuildMatchCenterState();
        state.MatchCenter.AutoplayEnabled = true;

        var advanced = ReplayAutoplay.AdvanceTick(state);

        Assert.True(advanced);
        Assert.Equal(1, state.MatchCenter.CurrentEventIndex);
        Assert.True(state.MatchCenter.AutoplayEnabled);
    }

    [Fact]
    public void AutoplayStopsOnReplayCompletion()
    {
        var state = BuildMatchCenterState();
        state.MatchCenter.CurrentEventIndex = state.MatchCenter.Replay!.EventLog.Count - 2;
        state.MatchCenter.AutoplayEnabled = true;

        var advanced = ReplayAutoplay.AdvanceTick(state);

        Assert.True(advanced);
        Assert.Equal(state.MatchCenter.Replay.EventLog.Count - 1, state.MatchCenter.CurrentEventIndex);
        Assert.False(state.MatchCenter.AutoplayEnabled);
    }

    [Fact]
    public void AutoplayDoesNotAdvanceWhileModalIsOpen()
    {
        var state = BuildMatchCenterState();
        state.MatchCenter.AutoplayEnabled = true;
        state.Modal = new ModalState { Title = "Paused", Lines = ["Blocked"] };

        var advanced = ReplayAutoplay.AdvanceTick(state);

        Assert.False(advanced);
        Assert.Equal(0, state.MatchCenter.CurrentEventIndex);
    }

    [Fact]
    public void TogglingAutoplayOffPausesProgression()
    {
        var state = BuildMatchCenterState();
        var controller = new MatchCenterController();

        controller.Handle(state, InputAction.ToggleAutoplay);
        Assert.True(state.MatchCenter.AutoplayEnabled);

        controller.Handle(state, InputAction.ToggleAutoplay);
        var advanced = ReplayAutoplay.AdvanceTick(state);

        Assert.False(state.MatchCenter.AutoplayEnabled);
        Assert.False(advanced);
        Assert.Equal(0, state.MatchCenter.CurrentEventIndex);
    }

    [Fact]
    public void InitialReplayStateUsesInitialSnapshot()
    {
        var replay = CreateReplay();

        var snapshot = MatchReplayNavigator.GetCurrentSnapshot(replay, 0);

        Assert.Equal("Initial snapshot", snapshot.BattleStateLabel);
        Assert.Equal(0, snapshot.LastLogIndexExclusive);
    }

    [Fact]
    public void FirstReplayStepAlignsWithNextSnapshotBoundary()
    {
        var replay = CreateReplay();

        var snapshot = MatchReplayNavigator.GetCurrentSnapshot(replay, 1);

        Assert.Equal("Round 1 snapshot", snapshot.BattleStateLabel);
        Assert.Equal(2, snapshot.LastLogIndexExclusive);
    }

    [Fact]
    public void RoundJumpAlignsWithNextRoundSnapshot()
    {
        var state = new MatchCenterState { Replay = CreateReplay() };

        MatchReplayNavigator.AdvanceRound(state);

        Assert.Equal(1, state.CurrentEventIndex);
        Assert.Equal("Round 1 snapshot", MatchReplayNavigator.GetCurrentSnapshot(state).BattleStateLabel);
    }

    [Fact]
    public void FinalReplayStateUsesFinalSnapshot()
    {
        var replay = CreateReplay();

        var snapshot = MatchReplayNavigator.GetCurrentSnapshot(replay, replay.EventLog.Count - 1);

        Assert.Equal("Final snapshot", snapshot.BattleStateLabel);
        Assert.Equal(replay.EventLog.Count, snapshot.LastLogIndexExclusive);
    }

    [Fact]
    public void ReplayUsesHistoricalFixtureSetupAfterDeckChanges()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var simulator = new WorldSimulator();
        var fixture = world.Season.Schedule.First(match => match.Week == world.Season.CurrentWeek && (world.Players[match.HomePlayerId].IsHuman || world.Players[match.AwayPlayerId].IsHuman));
        fixture.Result = simulator.SimulateMatch(world, fixture, (world.Season.Year * 1000) + world.Season.CurrentWeek + fixture.FixturePriority);
        var replayQueries = new MatchReplayQueries();
        var replayBeforeDeckChange = replayQueries.Build(world, fixture.Id)!;
        var player = world.Players[fixture.HomePlayerId];
        var originalChampionInstanceId = player.ActiveDeck!.ChampionInstanceId;
        var alternateChampion = player.Collection
            .First(card => world.GetLatestDefinition(card.CardId).Type == CardType.Champion && !string.Equals(card.InstanceId, originalChampionInstanceId, StringComparison.Ordinal));

        player.ActiveDeck = new SubmittedDeck
        {
            PlayerId = player.Id,
            ChampionInstanceId = alternateChampion.InstanceId,
            NonChampionInstanceIds = player.ActiveDeck.NonChampionInstanceIds.ToList(),
            SubmittedWeek = world.Season.CurrentWeek,
            Label = "Changed",
        };

        var replayAfterDeckChange = replayQueries.Build(world, fixture.Id)!;

        Assert.NotNull(fixture.ReplaySetup);
        Assert.Equal(replayBeforeDeckChange.EventLog, replayAfterDeckChange.EventLog);
        Assert.Equal(
            replayBeforeDeckChange.RoundSnapshots.Select(snapshot => (snapshot.BattleStateLabel, snapshot.LastLogIndexExclusive, HomeBoard: string.Join("|", snapshot.HomeBoardLines), AwayBoard: string.Join("|", snapshot.AwayBoardLines), snapshot.HomeCounterSummary, snapshot.AwayCounterSummary)),
            replayAfterDeckChange.RoundSnapshots.Select(snapshot => (snapshot.BattleStateLabel, snapshot.LastLogIndexExclusive, HomeBoard: string.Join("|", snapshot.HomeBoardLines), AwayBoard: string.Join("|", snapshot.AwayBoardLines), snapshot.HomeCounterSummary, snapshot.AwayCounterSummary)));
        Assert.Equal(world.Players[fixture.HomePlayerId].Collection.First(card => card.InstanceId == originalChampionInstanceId).CardId, fixture.ReplaySetup!.HomeDeck.ChampionCardId);
    }

    private static AppState BuildMatchCenterState()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        return new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
            ActiveScreen = ScreenId.MatchCenter,
            MatchCenter =
            {
                Replay = CreateReplay(),
            },
        };
    }

    private static MatchReplayViewModel CreateReplay()
    {
        return new MatchReplayViewModel(
            "MATCH-1",
            "Home vs Away",
            "Home",
            "Home wins 2-0",
            ["Seed: 1001", "Home champion: Veyn", "Away champion: Serah"],
            ["Event 0", "Event 1", "Event 2", "Event 3"],
            [
                CreateSnapshot(0, "Initial snapshot"),
                CreateSnapshot(2, "Round 1 snapshot"),
                CreateSnapshot(4, "Final snapshot")
            ]);
    }

    private static RoundSnapshotViewModel CreateSnapshot(int lastLogIndexExclusive, string label)
    {
        return new RoundSnapshotViewModel(
            1,
            "1",
            label,
            "Home",
            new ChampionPanelViewModel("Home", "Home", "Veyn", 20, 0),
            new ChampionPanelViewModel("Away", "Away", "Serah", 20, 0),
            [label],
            [label],
            "none",
            "none",
            lastLogIndexExclusive);
    }
}
