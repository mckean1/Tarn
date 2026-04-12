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
        var recentActivity = BuildRecentActivity(world, player);

        if (recentActivity.Count == 0)
        {
            recentActivity.Add("No recent activity yet. Advance the week or make a move.");
        }

        return new DashboardViewModel(
            Year: world.Season.Year,
            Week: world.Season.CurrentWeek,
            League: player.League.ToString(),
            Record: $"{standings.Wins}-{standings.Losses}",
            Rank: rank,
            Cash: player.Cash,
            DeckLegality: deckSummary.Legality,
            DeckSize: deckSummary.SizeSummary,
            NextMatch: nextMatch is null ? null : BuildNextMatch(world, player.Id, nextMatch),
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
            return ("NO DECK", $"0/{world.Config.Season.DeckSize}");
        }

        var validation = DeckValidator.ValidateSubmittedDeck(world, player, player.ActiveDeck);
        var size = player.ActiveDeck.NonChampionInstanceIds.Count + 1;
        return (validation.IsValid ? "LEGAL" : "INVALID", $"{size}/{world.Config.Season.DeckSize}");
    }

    private static DashboardNextMatchViewModel BuildNextMatch(World world, string humanPlayerId, Match match)
    {
        var opponentId = match.HomePlayerId == humanPlayerId ? match.AwayPlayerId : match.HomePlayerId;
        return new DashboardNextMatchViewModel(
            world.Players[opponentId].Name,
            match.League.ToString(),
            match.Week == world.Season.CurrentWeek ? "This Week" : $"Week {match.Week}");
    }

    private static List<string> BuildRecentActivity(World world, Player player)
    {
        var activities = new List<(int Week, int Priority, string Text)>();

        var lastMatch = world.Season.Schedule
            .Where(match => match.Result is not null && (match.HomePlayerId == player.Id || match.AwayPlayerId == player.Id))
            .OrderByDescending(match => match.Week)
            .ThenByDescending(match => match.FixturePriority)
            .FirstOrDefault();
        if (lastMatch is not null)
        {
            activities.Add((lastMatch.Week, 3, FormatRecentActivity(world, player.Id, lastMatch)));
        }

        var recentMarketEvent = world.MarketListings
            .Where(listing => string.Equals(listing.SellerPlayerId, player.Id, StringComparison.Ordinal) || listing.Bids.Any(bid => string.Equals(bid.PlayerId, player.Id, StringComparison.Ordinal)))
            .OrderByDescending(listing => Math.Max(listing.ExpiresWeek, listing.CreatedWeek))
            .ThenByDescending(listing => listing.Id, StringComparer.Ordinal)
            .Select(listing => BuildMarketActivity(world, player.Id, listing))
            .FirstOrDefault(activity => activity is not null);
        if (recentMarketEvent is not null)
        {
            activities.Add(recentMarketEvent.Value);
        }

        if (world.CollectorInventory.RefreshedWeek == world.Season.CurrentWeek)
        {
            activities.Add((world.CollectorInventory.RefreshedWeek, 1, $"Collector • refreshed for Week {world.Season.CurrentWeek}"));
        }

        return activities
            .OrderByDescending(activity => activity.Week)
            .ThenByDescending(activity => activity.Priority)
            .Select(activity => activity.Text)
            .Take(3)
            .ToList();
    }

    private static string FormatRecentActivity(World world, string humanPlayerId, Match match)
    {
        var won = match.Result!.WinnerPlayerId == humanPlayerId;
        var opponentId = match.HomePlayerId == humanPlayerId ? match.AwayPlayerId : match.HomePlayerId;
        return $"Match • W{match.Week} {(won ? "beat" : "lost to")} {world.Players[opponentId].Name} {match.Result.WinnerGameWins}-{match.Result.LoserGameWins}";
    }

    private static (int Week, int Priority, string Text)? BuildMarketActivity(World world, string humanPlayerId, MarketListing listing)
    {
        var definition = world.GetLatestDefinition(listing.CardId);
        if (string.Equals(listing.SellerPlayerId, humanPlayerId, StringComparison.Ordinal))
        {
            return listing.Status switch
            {
                ListingStatus.Sold => (listing.ExpiresWeek, 2, $"Market • sold {definition.Name}"),
                ListingStatus.Expired => (listing.ExpiresWeek, 2, $"Market • {definition.Name} expired"),
                ListingStatus.Active when listing.CreatedWeek == world.Season.CurrentWeek => (listing.CreatedWeek, 1, $"Market • listed {definition.Name}"),
                _ => null,
            };
        }

        var winningBid = listing.Bids
            .OrderByDescending(bid => bid.Amount)
            .ThenBy(bid => bid.PlayerId, StringComparer.Ordinal)
            .FirstOrDefault();
        return listing.Status == ListingStatus.Sold && winningBid is not null && string.Equals(winningBid.PlayerId, humanPlayerId, StringComparison.Ordinal)
            ? (listing.ExpiresWeek, 2, $"Auction • won {definition.Name} for {winningBid.Amount}")
            : null;
    }
}

public sealed record DashboardViewModel(
    int Year,
    int Week,
    string League,
    string Record,
    int Rank,
    int Cash,
    string DeckLegality,
    string DeckSize,
    DashboardNextMatchViewModel? NextMatch,
    IReadOnlyList<string> RecentActivity,
    IReadOnlyList<string> RecommendedActions);

public sealed record DashboardNextMatchViewModel(
    string Opponent,
    string League,
    string Status);
