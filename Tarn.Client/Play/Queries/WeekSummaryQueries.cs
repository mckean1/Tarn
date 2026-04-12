using Tarn.ClientApp.Play.Screens.WeekSummary;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class WeekSummaryQueries
{
    public WeekSummaryViewModel BuildDefault() =>
        new(
            "No Week Summary Yet",
            [
                "Advance the week from Dashboard to generate a fresh summary.",
                "This screen will become the landing page after each mutation.",
            ],
            null,
            ["Return to Dashboard"]);

    public WeekSummaryViewModel BuildAfterAdvance(World world, string humanPlayerId, int previousYear, int previousWeek, int previousCash, int previousWins, int previousLosses, int previousRank)
    {
        var player = world.Players[humanPlayerId];
        var standing = world.Season.Standings[humanPlayerId];
        var currentRank = StandingsCalculator.Rank(world.Season.Standings.Values.Where(entry => entry.League == player.League).ToList())
            .First(entry => entry.DeckId == humanPlayerId)
            .LeagueRank;
        var completedMatch = world.Season.Schedule
            .Where(match => match.Week == previousWeek && (match.HomePlayerId == humanPlayerId || match.AwayPlayerId == humanPlayerId))
            .FirstOrDefault();
        var replayMatchId = completedMatch?.Result is not null ? completedMatch.Id : null;
        var lines = new List<string>
        {
            $"Completed Year {previousYear}, Week {previousWeek}.",
            completedMatch?.Result is null ? "Your match is still pending." : FormatPlayerResult(world, humanPlayerId, completedMatch),
            $"Record: {previousWins}-{previousLosses} -> {standing.Wins}-{standing.Losses}",
            $"Rank: {FormatSignedDelta(previousRank - currentRank)} ({currentRank} now)",
            $"Cash: {FormatSignedDelta(player.Cash - previousCash)} ({player.Cash} total)",
            $"Collector refreshed for week {world.CollectorInventory.RefreshedWeek}.",
        };
        var actions = replayMatchId is null ? new[] { "Return to Dashboard" } : new[] { "Open Replay", "Return to Dashboard" };
        return new WeekSummaryViewModel($"Week {previousWeek} Complete", lines, replayMatchId, actions);
    }

    public static string FormatSignedDelta(int value) => value > 0 ? $"+{value}" : value.ToString();

    private static string FormatPlayerResult(World world, string humanPlayerId, Match match)
    {
        var won = match.Result!.WinnerPlayerId == humanPlayerId;
        var opponentId = match.HomePlayerId == humanPlayerId ? match.AwayPlayerId : match.HomePlayerId;
        return $"{(won ? "Victory" : "Defeat")} vs {world.Players[opponentId].Name} {match.Result.WinnerGameWins}-{match.Result.LoserGameWins}";
    }
}
