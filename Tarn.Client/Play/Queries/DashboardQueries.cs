using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class DashboardQueries
{
    public DashboardViewModel Build(World world, string humanPlayerId)
    {
        var player = world.Players[humanPlayerId];
        var standings = world.Season.Standings[humanPlayerId];
        var rank = StandingsCalculator.Rank(world.Season.Standings.Values.Where(entry => entry.League == player.League).ToList())
            .First(entry => entry.DeckId == humanPlayerId)
            .LeagueRank;
        var deckSummary = BuildDeckSummary(world, player);
        var nextMatch = world.Season.Schedule
            .Where(match => match.Week == world.Season.CurrentWeek)
            .FirstOrDefault(match => match.HomePlayerId == player.Id || match.AwayPlayerId == player.Id);
        var recentActivity = world.Season.Schedule
            .Where(match => match.Result is not null && (match.HomePlayerId == player.Id || match.AwayPlayerId == player.Id))
            .OrderByDescending(match => match.Week)
            .ThenByDescending(match => match.FixturePriority)
            .Take(3)
            .Select(match => FormatRecentActivity(world, player.Id, match))
            .ToList();

        if (recentActivity.Count == 0)
        {
            recentActivity.Add("No completed matches yet. Your first result is waiting to happen.");
        }

        return new DashboardViewModel(
            Year: world.Season.Year,
            Week: world.Season.CurrentWeek,
            League: player.League.ToString(),
            Record: $"{standings.Wins}-{standings.Losses}",
            RankLabel: $"Rank {rank}",
            Cash: player.Cash,
            DeckLegality: deckSummary.Legality,
            DeckSize: deckSummary.SizeSummary,
            NextMatchSummary: nextMatch is null ? "No fixture scheduled this week." : FormatNextMatch(world, player.Id, nextMatch),
            RecentActivity: recentActivity,
            RecommendedActions:
            [
                "Open Schedule",
                "Advance Week",
                nextMatch?.Result is not null ? "Open Latest Replay" : "Open Match Center",
                "Open Week Summary",
                "Visit Collector",
                "Visit Market",
            ]);
    }

    private static (string Legality, string SizeSummary) BuildDeckSummary(World world, Player player)
    {
        if (player.ActiveDeck is null)
        {
            return ("No active deck", "0/31 cards");
        }

        var validation = DeckValidator.ValidateSubmittedDeck(world, player, player.ActiveDeck);
        var size = player.ActiveDeck.NonChampionInstanceIds.Count + 1;
        return (validation.IsValid ? "LEGAL" : $"INVALID: {validation.Errors[0]}", $"{size}/{world.Config.Season.DeckSize} cards");
    }

    private static string FormatNextMatch(World world, string humanPlayerId, Match match)
    {
        var opponentId = match.HomePlayerId == humanPlayerId ? match.AwayPlayerId : match.HomePlayerId;
        return $"Next up: {world.Players[opponentId].Name} in {match.League}.";
    }

    private static string FormatRecentActivity(World world, string humanPlayerId, Match match)
    {
        var won = match.Result!.WinnerPlayerId == humanPlayerId;
        var opponentId = match.HomePlayerId == humanPlayerId ? match.AwayPlayerId : match.HomePlayerId;
        return $"Week {match.Week}: {(won ? "Won" : "Lost")} vs {world.Players[opponentId].Name} {match.Result.WinnerGameWins}-{match.Result.LoserGameWins}";
    }
}

public sealed record DashboardViewModel(
    int Year,
    int Week,
    string League,
    string Record,
    string RankLabel,
    int Cash,
    string DeckLegality,
    string DeckSize,
    string NextMatchSummary,
    IReadOnlyList<string> RecentActivity,
    IReadOnlyList<string> RecommendedActions);
