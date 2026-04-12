using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class LeagueQueries
{
    private static readonly LeagueTier[] LeagueOrder = [LeagueTier.Bronze, LeagueTier.Silver, LeagueTier.Gold, LeagueTier.World];

    public LeagueViewModel Build(World world, string humanPlayerId, int selectedLeagueOffset, int selectedIndex)
    {
        var human = world.Players[humanPlayerId];
        var baseIndex = Array.IndexOf(LeagueOrder, human.League);
        var leagueIndex = Math.Clamp(baseIndex + selectedLeagueOffset, 0, LeagueOrder.Length - 1);
        var league = LeagueOrder[leagueIndex];
        var ranked = StandingsCalculator.Rank(world.Season.Standings.Values.Where(entry => entry.League == league).ToList());
        var rows = ranked.Select((entry, index) =>
        {
            var player = world.Players[entry.DeckId];
            return new LeagueRowViewModel(
                entry.DeckId,
                entry.LeagueRank.ToString(),
                player.Name,
                $"{entry.Wins}-{entry.Losses}",
                entry.MatchPoints.ToString(),
                entry.GameDifferential.ToString(),
                BuildForm(world, entry.DeckId),
                player.IsHuman);
        }).ToList();

        var clampedIndex = rows.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, rows.Count - 1);
        var detail = rows.Count == 0 ? new LeagueDetailViewModel("No standings", ["No league data is available."], "No streak", "0", "0") : BuildDetail(world, league, rows[clampedIndex].PlayerId);
        return new LeagueViewModel(league.ToString(), leagueIndex - baseIndex, clampedIndex, rows, detail);
    }

    private LeagueDetailViewModel BuildDetail(World world, LeagueTier league, string playerId)
    {
        var ranked = StandingsCalculator.Rank(world.Season.Standings.Values.Where(entry => entry.League == league).ToList()).ToList();
        var selected = ranked.First(entry => entry.DeckId == playerId);
        var leaderPoints = ranked.First().MatchPoints;
        var index = ranked.FindIndex(entry => entry.DeckId == playerId);
        var recent = world.Season.Schedule
            .Where(match => match.Result is not null && (match.HomePlayerId == playerId || match.AwayPlayerId == playerId))
            .OrderByDescending(match => match.Week)
            .ThenByDescending(match => match.FixturePriority)
            .Take(3)
            .Select(match => FormatRecent(world, playerId, match))
            .ToList();
        if (recent.Count == 0)
        {
            recent.Add("No completed matches yet.");
        }

        var aheadBehind = index > 0
            ? $"{selected.MatchPoints - ranked[index - 1].MatchPoints} behind above"
            : "Leading the table";
        var below = index < ranked.Count - 1
            ? $"{selected.MatchPoints - ranked[index + 1].MatchPoints} ahead of below"
            : "Bottom edge";

        return new LeagueDetailViewModel(
            world.Players[playerId].Name,
            recent,
            CalculateStreak(world, playerId),
            (leaderPoints - selected.MatchPoints).ToString(),
            $"{aheadBehind} | {below}");
    }

    private static string BuildForm(World world, string playerId)
    {
        var form = world.Season.Schedule
            .Where(match => match.Result is not null && (match.HomePlayerId == playerId || match.AwayPlayerId == playerId))
            .OrderByDescending(match => match.Week)
            .ThenByDescending(match => match.FixturePriority)
            .Take(5)
            .Select(match => match.Result!.WinnerPlayerId == playerId ? "W" : "L");
        var text = string.Join("", form);
        return string.IsNullOrEmpty(text) ? "-" : text;
    }

    private static string CalculateStreak(World world, string playerId)
    {
        var results = world.Season.Schedule
            .Where(match => match.Result is not null && (match.HomePlayerId == playerId || match.AwayPlayerId == playerId))
            .OrderByDescending(match => match.Week)
            .ThenByDescending(match => match.FixturePriority)
            .Select(match => match.Result!.WinnerPlayerId == playerId)
            .ToList();
        if (results.Count == 0)
        {
            return "No streak";
        }

        var first = results[0];
        var count = results.TakeWhile(value => value == first).Count();
        return $"{(first ? "Won" : "Lost")} {count} straight";
    }

    private static string FormatRecent(World world, string playerId, Match match)
    {
        var opponent = world.Players[match.HomePlayerId == playerId ? match.AwayPlayerId : match.HomePlayerId].Name;
        return $"{(match.Result!.WinnerPlayerId == playerId ? "Beat" : "Lost to")} {opponent} in W{match.Week}";
    }
}

public sealed record LeagueViewModel(
    string LeagueName,
    int SelectedLeagueOffset,
    int SelectedIndex,
    IReadOnlyList<LeagueRowViewModel> Rows,
    LeagueDetailViewModel Detail);

public sealed record LeagueRowViewModel(
    string PlayerId,
    string Rank,
    string PlayerName,
    string Record,
    string MatchPoints,
    string GameDiff,
    string Form,
    bool IsHuman);

public sealed record LeagueDetailViewModel(
    string PlayerName,
    IReadOnlyList<string> RecentResults,
    string Streak,
    string PointsBehindLeader,
    string RivalGap);
