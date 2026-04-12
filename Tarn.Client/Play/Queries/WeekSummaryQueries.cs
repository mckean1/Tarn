using Tarn.ClientApp.Play.Screens.WeekSummary;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class WeekSummaryQueries
{
    public WeekSummaryViewModel BuildDefault() =>
        new(
            "No Summary Available",
            "Advance the week from Dashboard to generate the next campaign report.",
            [
                "After each week resolves, this screen will show your result, rewards, and notable updates.",
                "Replay access appears here once your latest match has been recorded.",
            ],
            [
                "Return to Dashboard when you are ready to progress the season.",
            ],
            false,
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
        var highlights = new List<string>
        {
            completedMatch?.Result is null ? "Result: Fixture still pending resolution." : $"Result: {FormatPlayerResult(world, humanPlayerId, completedMatch)}",
            $"Record: {previousWins}-{previousLosses} → {standing.Wins}-{standing.Losses}",
            $"Rank: {previousRank} → {currentRank}",
            $"Cash: {previousCash} → {player.Cash} ({FormatSignedDelta(player.Cash - previousCash)})",
        };

        var notes = new List<string>
        {
            $"Collector refreshed for Week {world.CollectorInventory.RefreshedWeek}: {world.CollectorInventory.Singles.Count} singles and {world.CollectorInventory.Packs.Count} packs available.",
        };

        if (BuildMarketOutcomeNote(world, humanPlayerId, previousWeek) is { } marketNote)
        {
            notes.Add(marketNote);
        }

        if (replayMatchId is not null)
        {
            notes.Add("Replay available from Week Summary or Match Center.");
        }

        var actions = replayMatchId is null ? new[] { "Return to Dashboard" } : new[] { "Open Replay", "Return to Dashboard" };
        return new WeekSummaryViewModel(
            $"Week {previousWeek} Complete",
            $"Year {previousYear} · Week {previousWeek} · {player.League} League · Cash {player.Cash}",
            highlights,
            notes,
            true,
            replayMatchId,
            actions);
    }

    public static string FormatSignedDelta(int value) => value > 0 ? $"+{value}" : value.ToString();

    private static string? BuildMarketOutcomeNote(World world, string humanPlayerId, int previousWeek)
    {
        var player = world.Players[humanPlayerId];
        var sellerSale = world.MarketListings
            .Where(listing => listing.ExpiresWeek == previousWeek)
            .Where(listing => string.Equals(listing.SellerPlayerId, humanPlayerId, StringComparison.Ordinal))
            .OrderBy(listing => listing.Id, StringComparer.Ordinal)
            .FirstOrDefault(listing => listing.Status == ListingStatus.Sold);
        if (sellerSale is not null)
        {
            var saleAmount = sellerSale.Bids
                .OrderByDescending(bid => bid.Amount)
                .ThenBy(bid => bid.PlayerId, StringComparer.Ordinal)
                .Select(bid => bid.Amount)
                .FirstOrDefault(sellerSale.MinimumBid);
            return $"Market: Sold {world.GetLatestDefinition(sellerSale.CardId).Name} for {saleAmount}.";
        }

        var buyerWin = world.MarketListings
            .Where(listing => listing.ExpiresWeek == previousWeek && listing.Status == ListingStatus.Sold)
            .Where(listing => !string.Equals(listing.SellerPlayerId, humanPlayerId, StringComparison.Ordinal))
            .OrderBy(listing => listing.Id, StringComparer.Ordinal)
            .FirstOrDefault(listing => listing.CardInstanceId is not null && player.Collection.Any(card => string.Equals(card.InstanceId, listing.CardInstanceId, StringComparison.Ordinal)));
        if (buyerWin is not null)
        {
            var winningBid = buyerWin.Bids.FirstOrDefault(bid => string.Equals(bid.PlayerId, humanPlayerId, StringComparison.Ordinal));
            var priceText = winningBid is null ? string.Empty : $" for {winningBid.Amount}";
            return $"Market: Won {world.GetLatestDefinition(buyerWin.CardId).Name}{priceText}.";
        }

        var expiredListing = world.MarketListings
            .Where(listing => listing.ExpiresWeek == previousWeek)
            .Where(listing => string.Equals(listing.SellerPlayerId, humanPlayerId, StringComparison.Ordinal))
            .OrderBy(listing => listing.Id, StringComparer.Ordinal)
            .FirstOrDefault(listing => listing.Status == ListingStatus.Expired);
        return expiredListing is null
            ? null
            : $"Market: {world.GetLatestDefinition(expiredListing.CardId).Name} expired without a sale.";
    }

    private static string FormatPlayerResult(World world, string humanPlayerId, Match match)
    {
        var won = match.Result!.WinnerPlayerId == humanPlayerId;
        var opponentId = match.HomePlayerId == humanPlayerId ? match.AwayPlayerId : match.HomePlayerId;
        var playerWins = won ? match.Result.WinnerGameWins : match.Result.LoserGameWins;
        var opponentWins = won ? match.Result.LoserGameWins : match.Result.WinnerGameWins;
        return $"{(won ? "Won" : "Lost")} vs {PlayerNameFormatter.Format(world, humanPlayerId, opponentId)} ({playerWins}-{opponentWins})";
    }
}
