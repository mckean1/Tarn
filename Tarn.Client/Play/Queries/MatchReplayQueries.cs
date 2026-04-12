using Tarn.ClientApp.Play.Screens.MatchCenter;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class MatchReplayQueries
{
    private readonly GameEngine engine = new();
    private readonly WorldSimulator simulator = new();

    public MatchReplayViewModel? Build(World world, string matchId)
    {
        var fixture = world.Season.Schedule.FirstOrDefault(match => match.Id == matchId);
        if (fixture?.Result is null)
        {
            return null;
        }

        var setup = BuildSetup(world, fixture);
        var state = engine.CreateMatchState(setup);
        var snapshots = new List<RoundSnapshotViewModel> { CaptureSnapshot(world, fixture, state, 0) };
        while (state.WinnerPlayerId is null)
        {
            engine.PlaySingleRound(state);
            snapshots.Add(CaptureSnapshot(world, fixture, state, state.ReplayLog.Count));
        }

        var title = $"{world.Players[fixture.HomePlayerId].Name} vs {world.Players[fixture.AwayPlayerId].Name}";
        var result = $"{world.Players[fixture.Result.WinnerPlayerId].Name} wins {fixture.Result.WinnerGameWins}-{fixture.Result.LoserGameWins}";
        return new MatchReplayViewModel(
            fixture.Id,
            title,
            $"Round 1 initiative: {world.Players[ResolveInitialPlayerId(fixture)].Name}",
            result,
            state.ReplayLog.Select(PhraseEvent).ToList(),
            snapshots);
    }

    public static string PhraseEvent(string raw)
    {
        var text = raw;
        var pipeIndex = text.IndexOf('|');
        if (pipeIndex >= 0)
        {
            text = text[(pipeIndex + 1)..].Trim();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text
            .Replace("P1", "Home", StringComparison.Ordinal)
            .Replace("P2", "Away", StringComparison.Ordinal);

        return text switch
        {
            var value when value.Contains("Enter Overtime", StringComparison.Ordinal) => "[OVERTIME] The match enters overtime.",
            var value when value.Contains("Fatigue", StringComparison.Ordinal) => "[FATIGUE] " + value,
            var value when value.Contains("counter", StringComparison.OrdinalIgnoreCase) => "[COUNTER] " + value,
            var value when value.Contains("Winner:", StringComparison.Ordinal) => "[FINISH] " + value,
            var value when value.Contains("takes", StringComparison.Ordinal) && value.Contains("falls to 0", StringComparison.Ordinal) => "[LETHAL] " + value,
            _ => text,
        };
    }

    private MatchSetup BuildSetup(World world, Match fixture)
    {
        var home = world.Players[fixture.HomePlayerId];
        var away = world.Players[fixture.AwayPlayerId];
        var homeDeck = home.ActiveDeck ?? throw new InvalidOperationException($"Player '{home.Id}' has no deck.");
        var awayDeck = away.ActiveDeck ?? throw new InvalidOperationException($"Player '{away.Id}' has no deck.");
        var homeChampion = (ChampionCardDefinition)world.GetLatestDefinition(home.Collection.First(card => card.InstanceId == homeDeck.ChampionInstanceId).CardId);
        var awayChampion = (ChampionCardDefinition)world.GetLatestDefinition(away.Collection.First(card => card.InstanceId == awayDeck.ChampionInstanceId).CardId);
        var homeCards = homeDeck.NonChampionInstanceIds.Select(id => world.GetLatestDefinition(home.Collection.First(card => card.InstanceId == id).CardId)).ToList();
        var awayCards = awayDeck.NonChampionInstanceIds.Select(id => world.GetLatestDefinition(away.Collection.First(card => card.InstanceId == id).CardId)).ToList();
        var seed = ResolveMatchSeed(world, fixture);

        return new MatchSetup
        {
            Seed = seed,
            PlayerOneId = fixture.HomePlayerId,
            PlayerTwoId = fixture.AwayPlayerId,
            PlayerOneDeck = new DeckDefinition(homeChampion, homeCards),
            PlayerTwoDeck = new DeckDefinition(awayChampion, awayCards),
            ShuffleDecks = true,
            Initiative = simulator.BuildInitiativeContext(fixture),
        };
    }

    private static int ResolveMatchSeed(World world, Match fixture)
    {
        var stepSeed = (fixture.Year * 1000) + fixture.Week;
        if (fixture.Year == world.Season.Year)
        {
            stepSeed = (fixture.Year * 1000) + fixture.Week;
        }

        return stepSeed + fixture.FixturePriority;
    }

    private static string ResolveInitialPlayerId(Match fixture)
    {
        if (fixture.Phase == MatchPhase.Playoffs && fixture.HomeSeed is not null && fixture.AwaySeed is not null)
        {
            return fixture.HomeSeed <= fixture.AwaySeed ? fixture.HomePlayerId : fixture.AwayPlayerId;
        }

        return fixture.FixturePriority % 2 == 1 ? fixture.HomePlayerId : fixture.AwayPlayerId;
    }

    private static RoundSnapshotViewModel CaptureSnapshot(World world, Match fixture, MatchState state, int lastLogIndexExclusive)
    {
        var playerOne = state.PlayerOne;
        var playerTwo = state.PlayerTwo;
        return new RoundSnapshotViewModel(
            Math.Max(0, state.RoundNumber),
            state.WinnerPlayerId is not null ? "Match complete" : state.OvertimePending ? "Overtime pending" : "Battle in progress",
            new ChampionPanelViewModel(world.Players[fixture.HomePlayerId].Name, playerOne.Champion.Health, playerOne.FatigueCount),
            new ChampionPanelViewModel(world.Players[fixture.AwayPlayerId].Name, playerTwo.Champion.Health, playerTwo.FatigueCount),
            BuildBattlefieldLines(playerOne, playerTwo),
            BuildCounterLines(playerOne, playerTwo),
            lastLogIndexExclusive);
    }

    private static IReadOnlyList<string> BuildBattlefieldLines(PlayerState one, PlayerState two)
    {
        return
        [
            $"Home board: {FormatUnits(one.Battlefield)}",
            $"Away board: {FormatUnits(two.Battlefield)}",
        ];
    }

    private static IReadOnlyList<string> BuildCounterLines(PlayerState one, PlayerState two)
    {
        return
        [
            $"Home counters: {FormatCounters(one.CounterZone)}",
            $"Away counters: {FormatCounters(two.CounterZone)}",
        ];
    }

    private static string FormatUnits(IReadOnlyList<UnitState> units)
    {
        if (units.Count == 0)
        {
            return "empty";
        }

        return string.Join(", ", units.OrderBy(unit => unit.ZoneOrder).Select(unit => $"{unit.Card.Name} {unit.Attack}/{unit.Health}"));
    }

    private static string FormatCounters(IReadOnlyList<CounterState> counters)
    {
        if (counters.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", counters.OrderBy(counter => counter.ZoneOrder).Select(counter => counter.Card.Name));
    }
}
