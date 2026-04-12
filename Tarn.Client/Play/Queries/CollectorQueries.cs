using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public enum CollectorTab
{
    Singles,
    Packs,
    Sell,
}

public sealed class CollectorQueries
{
    public CollectorViewModel Build(World world, string humanPlayerId, CollectorTab tab, int selectedIndex)
    {
        var player = world.Players[humanPlayerId];
        var ownedCounts = player.Collection
            .GroupBy(card => card.CardId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var rows = tab switch
        {
            CollectorTab.Singles => world.CollectorInventory.Singles
                .OrderBy(item => item.Price)
                .Select(item =>
                {
                    var definition = world.GetLatestDefinition(item.CardId);
                    var status = BuildSingleStatus(player.Cash, item.Price, ownedCounts.GetValueOrDefault(item.CardId));
                    var ownedLabel = ownedCounts.TryGetValue(item.CardId, out var owned) && owned > 0 ? $"Owned: {owned}" : null;
                    return new CollectorRowViewModel(
                        item.ListingId,
                        definition.Name,
                        definition.Type.ToString(),
                        definition.Rarity.ToString(),
                        item.Price,
                        status,
                        item.IsLegendaryReveal ? "Revealed legendary offer" : "Collector single",
                        string.IsNullOrWhiteSpace(definition.RulesText) ? "No rules text." : definition.RulesText,
                        CardTextFormatter.BuildCollectionStats(definition),
                        CardTextFormatter.BuildKeywordSummary(definition.Keywords),
                        $"Buy for {item.Price}",
                        $"Buy for {item.Price}",
                        ownedLabel);
                })
                .ToList(),
            CollectorTab.Packs => world.CollectorInventory.Packs
                .OrderBy(item => item.Price)
                .Select(item => new CollectorRowViewModel(
                    item.ProductId,
                    $"{world.CardSets[item.SetId].Name} Pack",
                    "Pack",
                    string.Empty,
                    item.Price,
                    FormatAffordability(player.Cash, item.Price),
                    "Contains 10 cards · Common-heavy",
                    "Contains 5 Commons, 3 Rares, 2 Epics, with a chance to upgrade an Epic slot into a Legendary.",
                    null,
                    "N/A",
                    $"Open for {item.Price}",
                    $"Open for {item.Price}",
                    null))
                .ToList(),
            _ => CardDisplayGrouper.GroupOwnedCards(world, player.Collection.Where(card => !card.IsListed && !card.PendingSettlement))
                .Select(group =>
                {
                    var definition = world.GetLatestDefinition(group.CardId);
                    var first = player.Collection
                        .Where(card => !card.IsListed && !card.PendingSettlement)
                        .First(card => string.Equals(card.CardId, group.CardId, StringComparison.Ordinal));
                    var buybackPrice = CollectorService.GetCollectorBuybackPrice(world, definition.Id);
                    return new CollectorRowViewModel(
                        first.InstanceId,
                        definition.Name,
                        definition.Type.ToString(),
                        definition.Rarity.ToString(),
                        buybackPrice,
                        "Sellable",
                        $"Owned: {group.Count}",
                        string.IsNullOrWhiteSpace(definition.RulesText) ? "No rules text." : definition.RulesText,
                        CardTextFormatter.BuildCollectionStats(definition),
                        CardTextFormatter.BuildKeywordSummary(definition.Keywords),
                        $"Sell for {buybackPrice}",
                        $"Sell for {buybackPrice}",
                        $"Owned: {group.Count}",
                        group.Count);
                })
                .ToList(),
        };

        var clampedIndex = rows.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, rows.Count - 1);
        return new CollectorViewModel(tab, clampedIndex, rows, rows.Count == 0 ? null : rows[clampedIndex]);
    }

    public static string FormatAffordability(int cash, int price) => cash >= price ? "Affordable" : "Not enough cash";

    public static string FormatPackReveal(IReadOnlyList<PackRevealCard> cards)
    {
        var newCount = cards.Count(card => card.IsNewCardId);
        var dupeCount = cards.Count - newCount;
        var lines = new List<string> { $"Pack opened: {newCount} new, {dupeCount} dupes" };
        lines.AddRange(CardDisplayGrouper.GroupPackRevealCards(cards)
            .Select(group =>
            {
                var newCopies = cards.Count(card => string.Equals(card.CardId, group.CardId, StringComparison.Ordinal) && card.IsNewCardId);
                var status = newCopies == group.Count
                    ? "NEW"
                    : newCopies == 0
                        ? "DUPE"
                        : $"{newCopies} new";
                return $"[{group.Rarity}] {group.DisplayName} {status}";
            }));
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildSingleStatus(int cash, int price, int ownedCount)
    {
        if (cash < price)
        {
            return "Not enough cash";
        }

        return ownedCount > 0 ? "Owned" : "Affordable";
    }
}

public sealed record CollectorViewModel(
    CollectorTab Tab,
    int SelectedIndex,
    IReadOnlyList<CollectorRowViewModel> Rows,
    CollectorRowViewModel? Detail);

public sealed record CollectorRowViewModel(
    string ReferenceId,
    string Name,
    string Type,
    string Rarity,
    int Price,
    string Status,
    string SummaryText,
    string RulesText,
    string? StatsText,
    string KeywordsText,
    string PriceLabel,
    string ActionLabel,
    string? OwnedLabel,
    int Quantity = 1)
{
    public string DisplayName => CardDisplayGrouper.FormatDisplayName(Name, Quantity);
}

public sealed record PackRevealCard(string CardId, string Name, CardType Type, CardRarity Rarity, bool IsNewCardId);
