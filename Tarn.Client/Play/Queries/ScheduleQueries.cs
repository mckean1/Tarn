using Tarn.ClientApp.Play.Screens.Schedule;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class ScheduleQueries
{
    public ScheduleViewModel Build(World world, int selectedWeek, int selectedIndex, string humanPlayerId)
    {
        var focusLeague = world.Players[humanPlayerId].League;
        var fixtures = world.Season.Schedule
            .Where(match => match.Week == selectedWeek && match.League == focusLeague)
            .OrderBy(match => IsPlayerFixture(match, humanPlayerId) ? 0 : 1)
            .ThenBy(match => match.FixturePriority)
            .Select(match => new ScheduleFixtureItem(
                match.Id,
                match.League.ToString(),
                FormatPairing(world, humanPlayerId, match),
                FormatStatus(world, match),
                FormatResult(match),
                match.Result is not null,
                IsPlayerFixture(match, humanPlayerId)))
            .ToList();

        var clampedIndex = fixtures.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, fixtures.Count - 1);
        var detail = BuildSelectionDetail(world, selectedWeek, fixtures, clampedIndex, humanPlayerId);
        return new ScheduleViewModel(selectedWeek, clampedIndex, focusLeague.ToString(), fixtures, detail);
    }

    public ScheduleFixtureDetail BuildSelectionDetail(World world, int selectedWeek, IReadOnlyList<ScheduleFixtureItem> fixtures, int selectedIndex, string humanPlayerId)
    {
        if (fixtures.Count == 0)
        {
            var focusLeague = world.Players[humanPlayerId].League;
            return new ScheduleFixtureDetail(
                string.Empty,
                "Selected Fixture",
                [
                    $"Week: {selectedWeek}",
                    $"League: {focusLeague}",
                    "Status: No fixtures",
                    "Replay: Not available",
                    "Result: No fixture selected",
                    "Focus: Change week to browse another fixture set.",
                ],
                false);
        }

        var clampedIndex = Math.Clamp(selectedIndex, 0, fixtures.Count - 1);
        return BuildDetail(world, fixtures[clampedIndex].MatchId, humanPlayerId);
    }

    public ScheduleFixtureDetail BuildDetail(World world, string matchId, string humanPlayerId)
    {
        var match = world.Season.Schedule.First(item => item.Id == matchId);
        var replayAvailable = match.Result is not null;
        var lines = new List<string>
        {
            $"Match: {FormatPairing(world, humanPlayerId, match)}",
            $"Week: {match.Week}",
            $"League: {match.League}",
            $"Status: {FormatStatus(world, match)}",
            $"Replay: {(replayAvailable ? "Available" : "Not available")}",
            match.Result is null
                ? "Result: Not played"
                : $"Result: {FormatPlayerName(world, humanPlayerId, match.Result.WinnerPlayerId)} won {match.Result.WinnerGameWins}-{match.Result.LoserGameWins}",
            IsPlayerFixture(match, humanPlayerId) ? "Focus: Your match" : "Focus: League fixture",
            replayAvailable ? "Action: Press Enter to open replay." : "Action: Replay unlocks when the match is complete.",
        };
        return new ScheduleFixtureDetail(match.Id, "Selected Fixture", lines, replayAvailable);
    }

    private static bool IsPlayerFixture(Match match, string humanPlayerId) =>
        match.HomePlayerId == humanPlayerId || match.AwayPlayerId == humanPlayerId;

    private static string FormatPairing(World world, string humanPlayerId, Match match) =>
        $"{FormatPlayerName(world, humanPlayerId, match.HomePlayerId)} vs {FormatPlayerName(world, humanPlayerId, match.AwayPlayerId)}";

    private static string FormatPlayerName(World world, string humanPlayerId, string playerId) =>
        playerId == humanPlayerId ? "You" : world.Players[playerId].Name;

    private static string FormatStatus(World world, Match match)
    {
        if (match.Result is not null)
        {
            return "Complete";
        }

        return match.Week > world.Season.CurrentWeek ? "Upcoming" : "Pending";
    }

    private static string FormatResult(Match match) =>
        match.Result is null ? string.Empty : $"{match.Result.WinnerGameWins}-{match.Result.LoserGameWins}";
}

public sealed record ScheduleViewModel(
    int SelectedWeek,
    int SelectedFixtureIndex,
    string FocusLeagueLabel,
    IReadOnlyList<ScheduleFixtureItem> Fixtures,
    ScheduleFixtureDetail Detail);
