using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public static class CardDisplayGrouper
{
    public static IReadOnlyList<GroupedCardDisplayEntry> GroupOwnedCards(World world, IEnumerable<OwnedCard> cards)
        => Group(
            cards,
            card => card.CardId,
            card => world.GetLatestDefinition(card.CardId).Name,
            card => world.GetLatestDefinition(card.CardId).Type,
            card => world.GetLatestDefinition(card.CardId).Rarity);

    public static IReadOnlyList<GroupedCardDisplayEntry> GroupCardIds(World world, IEnumerable<string> cardIds)
        => Group(
            cardIds,
            cardId => cardId,
            cardId => world.GetLatestDefinition(cardId).Name,
            cardId => world.GetLatestDefinition(cardId).Type,
            cardId => world.GetLatestDefinition(cardId).Rarity);

    public static IReadOnlyList<GroupedCardDisplayEntry> GroupPackRevealCards(IEnumerable<PackRevealCard> cards)
        => Group(cards, card => card.CardId, card => card.Name, card => card.Type, card => card.Rarity);

    public static string FormatDisplayName(string name, int count) => $"{name} x{count}";

    public static int GetTypeSortOrder(CardType type) => type switch
    {
        CardType.Champion => 0,
        CardType.Unit => 1,
        CardType.Spell => 2,
        CardType.Counter => 3,
        _ => 4,
    };

    public static int GetRaritySortOrder(CardRarity rarity) => rarity switch
    {
        CardRarity.Common => 0,
        CardRarity.Rare => 1,
        CardRarity.Epic => 2,
        CardRarity.Legendary => 3,
        _ => 4,
    };

    private static IReadOnlyList<GroupedCardDisplayEntry> Group<T>(
        IEnumerable<T> items,
        Func<T, string> cardIdSelector,
        Func<T, string> nameSelector,
        Func<T, CardType> typeSelector,
        Func<T, CardRarity> raritySelector)
    {
        return items
            .GroupBy(cardIdSelector, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return new GroupedCardDisplayEntry(group.Key, nameSelector(first), typeSelector(first), raritySelector(first), group.Count());
            })
            .OrderBy(entry => GetTypeSortOrder(entry.Type))
            .ThenBy(entry => GetRaritySortOrder(entry.Rarity))
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
    }
}

public sealed record GroupedCardDisplayEntry(
    string CardId,
    string Name,
    CardType Type,
    CardRarity Rarity,
    int Count)
{
    public string DisplayName => CardDisplayGrouper.FormatDisplayName(Name, Count);
}
