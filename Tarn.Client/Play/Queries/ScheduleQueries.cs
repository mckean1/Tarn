using Tarn.ClientApp.Play.Screens.Schedule;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class ScheduleQueries
{
    public ScheduleViewModel Build(World world, int selectedWeek, int selectedIndex, string humanPlayerId)
    {
        var fixtures = world.Season.Schedule
            .Where(match => match.Week == selectedWeek)
            .OrderBy(match => match.League)
            .ThenBy(match => match.FixturePriority)
            .Select(match => new ScheduleFixtureItem(
                match.Id,
                FormatSummary(world, humanPlayerId, match),
                match.Result is not null))
            .ToList();

        var clampedIndex = fixtures.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, fixtures.Count - 1);
        var detail = fixtures.Count == 0 ? new ScheduleFixtureDetail(string.Empty, $"Week {selectedWeek}", ["No fixtures are scheduled in this window."], false) : BuildDetail(world, fixtures[clampedIndex].MatchId, humanPlayerId);
        return new ScheduleViewModel(selectedWeek, clampedIndex, fixtures, detail);
    }

    public ScheduleFixtureDetail BuildDetail(World world, string matchId, string humanPlayerId)
    {
        var match = world.Season.Schedule.First(item => item.Id == matchId);
        var home = world.Players[match.HomePlayerId].Name;
        var away = world.Players[match.AwayPlayerId].Name;
        var lines = new List<string>
        {
            $"{home} vs {away}",
            $"League: {match.League} | Week {match.Week}",
            match.Result is null ? "Result: Pending" : $"Result: {world.Players[match.Result.WinnerPlayerId].Name} def. {world.Players[match.Result.LoserPlayerId].Name} {match.Result.WinnerGameWins}-{match.Result.LoserGameWins}",
            match.HomePlayerId == humanPlayerId || match.AwayPlayerId == humanPlayerId ? "Your fixture" : "League fixture",
        };
        return new ScheduleFixtureDetail(match.Id, $"Week {match.Week} Fixture", lines, match.Result is not null);
    }

    private static string FormatSummary(World world, string humanPlayerId, Match match)
    {
        var home = world.Players[match.HomePlayerId].Name;
        var away = world.Players[match.AwayPlayerId].Name;
        var marker = match.HomePlayerId == humanPlayerId || match.AwayPlayerId == humanPlayerId ? "*" : " ";
        var result = match.Result is null ? "Pending" : $"{match.Result.WinnerGameWins}-{match.Result.LoserGameWins}";
        return $"{marker} {match.League,-6} {home} vs {away} [{result}]";
    }
}

public sealed record ScheduleViewModel(
    int SelectedWeek,
    int SelectedFixtureIndex,
    IReadOnlyList<ScheduleFixtureItem> Fixtures,
    ScheduleFixtureDetail Detail);
