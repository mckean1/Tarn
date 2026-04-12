using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public enum CollectionFilter
{
    All,
    Units,
    Spells,
    Counters,
    Champions,
    OwnedOnly,
}

public enum CollectionSort
{
    Name,
    Type,
    Rarity,
    OwnedCount,
}

public sealed class CollectionQueries
{
    public CollectionViewModel Build(World world, string humanPlayerId, CollectionFilter filter, CollectionSort sort, int selectedIndex)
    {
        var player = world.Players[humanPlayerId];
        var activeDeckCounts = player.ActiveDeck is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : player.ActiveDeck.NonChampionInstanceIds.Append(player.ActiveDeck.ChampionInstanceId)
                .Select(id => player.Collection.First(card => card.InstanceId == id).CardId)
                .GroupBy(id => id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var grouped = CardDisplayGrouper.GroupOwnedCards(world, player.Collection)
            .Select(group =>
            {
                var definition = world.GetLatestDefinition(group.CardId);
                activeDeckCounts.TryGetValue(group.CardId, out var used);
                return new CollectionRowViewModel(
                    definition.Id,
                    definition.Name,
                    definition.Type,
                    definition.Rarity,
                    group.Count,
                    used,
                    CardTextFormatter.BuildCollectionStats(definition),
                    CardTextFormatter.BuildKeywordSummary(definition.Keywords),
                    string.IsNullOrWhiteSpace(definition.RulesText) ? "No rules text." : definition.RulesText);
            })
            .Where(row => MatchesFilter(row, filter))
            .ToList();

        grouped = Sort(grouped, sort).ToList();
        var clampedIndex = grouped.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, grouped.Count - 1);
        var detail = grouped.Count == 0
            ? null
            : BuildDetail(grouped[clampedIndex]);
        return new CollectionViewModel(GetFilterLabel(filter), GetSortLabel(sort), clampedIndex, grouped, detail);
    }

    public static IEnumerable<CollectionRowViewModel> Sort(IEnumerable<CollectionRowViewModel> rows, CollectionSort sort)
    {
        return sort switch
        {
            CollectionSort.Type => rows.OrderBy(row => CardDisplayGrouper.GetTypeSortOrder(row.Type)).ThenBy(row => CardDisplayGrouper.GetRaritySortOrder(row.Rarity)).ThenBy(row => row.Name, StringComparer.Ordinal),
            CollectionSort.Rarity => rows.OrderBy(row => CardDisplayGrouper.GetRaritySortOrder(row.Rarity)).ThenBy(row => CardDisplayGrouper.GetTypeSortOrder(row.Type)).ThenBy(row => row.Name, StringComparer.Ordinal),
            CollectionSort.OwnedCount => rows.OrderByDescending(row => row.OwnedCount).ThenBy(row => CardDisplayGrouper.GetTypeSortOrder(row.Type)).ThenBy(row => CardDisplayGrouper.GetRaritySortOrder(row.Rarity)).ThenBy(row => row.Name, StringComparer.Ordinal),
            _ => rows.OrderBy(row => CardDisplayGrouper.GetTypeSortOrder(row.Type)).ThenBy(row => CardDisplayGrouper.GetRaritySortOrder(row.Rarity)).ThenBy(row => row.Name, StringComparer.Ordinal),
        };
    }

    public static bool MatchesFilter(CollectionRowViewModel row, CollectionFilter filter)
    {
        return filter switch
        {
            CollectionFilter.Units => row.Type == CardType.Unit,
            CollectionFilter.Spells => row.Type == CardType.Spell,
            CollectionFilter.Counters => row.Type == CardType.Counter,
            CollectionFilter.Champions => row.Type == CardType.Champion,
            CollectionFilter.OwnedOnly => row.OwnedCount > 0,
            _ => true,
        };
    }

    public static string GetFilterLabel(CollectionFilter filter) => filter switch
    {
        CollectionFilter.OwnedOnly => "Owned Only",
        _ => filter.ToString(),
    };

    public static string GetSortLabel(CollectionSort sort) => sort switch
    {
        CollectionSort.Name => "Type / Rarity / Name",
        CollectionSort.OwnedCount => "Owned Count",
        _ => sort.ToString(),
    };

    private static CollectionDetailViewModel BuildDetail(CollectionRowViewModel row)
        => new(
            row.Name,
            row.Type.ToString(),
            row.Rarity.ToString(),
            row.StatsText,
            row.OwnedCount,
            row.UsedInDeckCount,
            row.KeywordsText,
            row.RulesText);
}

public sealed record CollectionViewModel(
    string FilterLabel,
    string SortLabel,
    int SelectedIndex,
    IReadOnlyList<CollectionRowViewModel> Rows,
    CollectionDetailViewModel? Detail);

public sealed record CollectionRowViewModel(
    string CardId,
    string Name,
    CardType Type,
    CardRarity Rarity,
    int OwnedCount,
    int UsedInDeckCount,
    string? StatsText,
    string KeywordsText,
    string RulesText)
{
    public string DisplayName => CardDisplayGrouper.FormatDisplayName(Name, OwnedCount);
    public string TypeLabel => Type.ToString();
    public string RarityLabel => Rarity.ToString();
}

public sealed record CollectionDetailViewModel(
    string Name,
    string Type,
    string Rarity,
    string? StatsText,
    int OwnedCount,
    int UsedInDeckCount,
    string KeywordsText,
    string RulesText);
