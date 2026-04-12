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
        var rows = tab switch
        {
            CollectorTab.Singles => world.CollectorInventory.Singles
                .OrderBy(item => item.Price)
                .Select(item =>
                {
                    var definition = world.GetLatestDefinition(item.CardId);
                    return new CollectorRowViewModel(
                        item.ListingId,
                        definition.Name,
                        definition.Type.ToString(),
                        definition.Rarity.ToString(),
                        item.Price,
                        FormatAffordability(player.Cash, item.Price),
                        definition.RulesText,
                        CardTextFormatter.BuildStats(definition),
                        $"Buy for {item.Price}",
                        item.IsLegendaryReveal ? "Revealed legendary offer" : "Collector single");
                })
                .ToList(),
            CollectorTab.Packs => world.CollectorInventory.Packs
                .OrderBy(item => item.Price)
                .Select(item => new CollectorRowViewModel(
                    item.ProductId,
                    $"{world.CardSets[item.SetId].Name} Pack",
                    "Pack",
                    item.SetId,
                    item.Price,
                    FormatAffordability(player.Cash, item.Price),
                    "Contains 5 Commons, 3 Rares, 2 Epics with a chance to upgrade an Epic slot into a Legendary.",
                    "Pack contents",
                    $"Open for {item.Price}",
                    "Opening adds 10 cards to your collection."))
                .ToList(),
            _ => player.Collection
                .Where(card => !card.IsListed && !card.PendingSettlement)
                .GroupBy(card => card.CardId, StringComparer.Ordinal)
                .OrderBy(group => world.GetLatestDefinition(group.Key).Name, StringComparer.Ordinal)
                .Select(group =>
                {
                    var definition = world.GetLatestDefinition(group.Key);
                    var first = group.First();
                    return new CollectorRowViewModel(
                        first.InstanceId,
                        definition.Name,
                        definition.Type.ToString(),
                        definition.Rarity.ToString(),
                        CollectorService.GetCollectorBuybackPrice(world, definition.Id),
                        "Available",
                        definition.RulesText,
                        CardTextFormatter.BuildStats(definition),
                        $"Sell for {CollectorService.GetCollectorBuybackPrice(world, definition.Id)}",
                        $"Owned {group.Count()} copies");
                })
                .ToList(),
        };

        var clampedIndex = rows.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, rows.Count - 1);
        return new CollectorViewModel(tab, clampedIndex, rows, rows.Count == 0 ? null : rows[clampedIndex]);
    }

    public static string FormatAffordability(int cash, int price) => cash >= price ? "Affordable" : "Too Expensive";

    public static string FormatPackReveal(IReadOnlyList<PackRevealCard> cards)
    {
        var newCount = cards.Count(card => card.IsNewCardId);
        var dupeCount = cards.Count - newCount;
        var lines = new List<string> { $"Pack opened: {newCount} new, {dupeCount} dupes" };
        lines.AddRange(cards.Select(card => $"[{card.Rarity}] {card.Name} {(card.IsNewCardId ? "NEW" : "DUPE")}"));
        return string.Join(Environment.NewLine, lines);
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
    string RulesText,
    string StatsText,
    string PriceLabel,
    string ImpactText);

public sealed record PackRevealCard(string Name, string Rarity, bool IsNewCardId);
