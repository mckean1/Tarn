using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public enum MarketTab
{
    Browse,
    MyListings,
    CreateListing,
}

public sealed class MarketQueries
{
    public MarketViewModel Build(World world, string humanPlayerId, MarketTab tab, int selectedIndex, int proposedBidOrPrice, int proposedDurationWeeks)
    {
        var player = world.Players[humanPlayerId];
        var rows = tab switch
        {
            MarketTab.Browse => world.MarketListings
                .Where(item => item.Status == ListingStatus.Active)
                .OrderBy(item => item.ExpiresWeek)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => BuildBrowseRow(world, humanPlayerId, item))
                .ToList(),
            MarketTab.MyListings => world.MarketListings
                .Where(item => item.SellerPlayerId == humanPlayerId)
                .OrderByDescending(item => item.CreatedWeek)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => BuildMyListingRow(world, item))
                .ToList(),
            _ => player.Collection
                .Where(card => !card.IsListed && !card.PendingSettlement)
                .OrderBy(card => world.GetLatestDefinition(card.CardId).Name, StringComparer.Ordinal)
                .Select(card =>
                {
                    var definition = world.GetLatestDefinition(card.CardId);
                    return new MarketRowViewModel(
                        card.InstanceId,
                        definition.Name,
                        "You",
                        "-",
                        "-",
                        "-",
                        "Ready",
                        definition.RulesText,
                        CardTextFormatter.BuildStats(definition));
                })
                .ToList(),
        };

        var clampedIndex = rows.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, rows.Count - 1);
        var detail = rows.Count == 0 ? null : rows[clampedIndex];
        var resolvedPrice = proposedBidOrPrice > 0 ? proposedBidOrPrice : ResolveDefaultPrice(tab, rows, clampedIndex);
        return new MarketViewModel(tab, clampedIndex, rows, detail, resolvedPrice, Math.Max(1, proposedDurationWeeks));
    }

    public static string FormatTimeLeft(World world, MarketListing listing)
    {
        var total = world.Config.Season.TotalWeeks;
        var delta = listing.ExpiresWeek - world.Season.CurrentWeek;
        if (delta < 0)
        {
            delta += total;
        }

        return delta == 0 ? "Ends this week" : $"{delta}w left";
    }

    public static string FormatStatus(MarketListing listing) => listing.Status switch
    {
        ListingStatus.Active => "Active",
        ListingStatus.Sold => "Sold",
        ListingStatus.Expired => "Expired",
        _ => listing.Status.ToString(),
    };

    private static int ResolveDefaultPrice(MarketTab tab, IReadOnlyList<MarketRowViewModel> rows, int index)
    {
        if (rows.Count == 0)
        {
            return 1;
        }

        var row = rows[Math.Clamp(index, 0, rows.Count - 1)];
        return int.TryParse(row.CurrentBid, out var price) && price > 0 ? price : 1;
    }

    private static MarketRowViewModel BuildBrowseRow(World world, string humanPlayerId, MarketListing item)
    {
        var definition = world.GetLatestDefinition(item.CardId);
        return new MarketRowViewModel(
            item.Id,
            definition.Name,
            world.Players[item.SellerPlayerId!].Name,
            MarketService.GetNextBidAmount(item).ToString(),
            FormatTimeLeft(world, item),
            item.Bids.Count.ToString(),
            MarketService.GetAvailableCashForBids(world, humanPlayerId, item.Id) >= MarketService.GetNextBidAmount(item) ? "Can Bid" : "Short Cash",
            definition.RulesText,
            CardTextFormatter.BuildStats(definition));
    }

    private static MarketRowViewModel BuildMyListingRow(World world, MarketListing item)
    {
        var definition = world.GetLatestDefinition(item.CardId);
        return new MarketRowViewModel(
            item.Id,
            definition.Name,
            "You",
            item.Bids.Count == 0 ? "-" : item.Bids.Max(bid => bid.Amount).ToString(),
            FormatTimeLeft(world, item),
            item.Bids.Count.ToString(),
            FormatStatus(item),
            definition.RulesText,
            CardTextFormatter.BuildStats(definition));
    }
}

public sealed record MarketViewModel(
    MarketTab Tab,
    int SelectedIndex,
    IReadOnlyList<MarketRowViewModel> Rows,
    MarketRowViewModel? Detail,
    int ProposedBidOrPrice,
    int ProposedDurationWeeks);

public sealed record MarketRowViewModel(
    string ReferenceId,
    string CardName,
    string Seller,
    string CurrentBid,
    string TimeLeft,
    string BidCount,
    string Status,
    string RulesText,
    string StatsText);
