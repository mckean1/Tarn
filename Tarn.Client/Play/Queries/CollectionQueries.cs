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

        var grouped = player.Collection
            .GroupBy(card => card.CardId, StringComparer.Ordinal)
            .Select(group =>
            {
                var definition = world.GetLatestDefinition(group.Key);
                activeDeckCounts.TryGetValue(group.Key, out var used);
                return new CollectionRowViewModel(
                    definition.Id,
                    definition.Name,
                    definition.Type.ToString(),
                    definition.Rarity.ToString(),
                    group.Count(),
                    used,
                    definition.RulesText,
                    CardTextFormatter.BuildStats(definition));
            })
            .Where(row => MatchesFilter(row, filter))
            .ToList();

        grouped = Sort(grouped, sort).ToList();
        var clampedIndex = grouped.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, grouped.Count - 1);
        var detail = grouped.Count == 0 ? null : grouped[clampedIndex];
        return new CollectionViewModel(filter, sort, clampedIndex, grouped, detail);
    }

    public static IEnumerable<CollectionRowViewModel> Sort(IEnumerable<CollectionRowViewModel> rows, CollectionSort sort)
    {
        return sort switch
        {
            CollectionSort.Type => rows.OrderBy(row => row.Type).ThenBy(row => row.Name, StringComparer.Ordinal),
            CollectionSort.Rarity => rows.OrderBy(row => row.Rarity).ThenBy(row => row.Name, StringComparer.Ordinal),
            CollectionSort.OwnedCount => rows.OrderByDescending(row => row.OwnedCount).ThenBy(row => row.Name, StringComparer.Ordinal),
            _ => rows.OrderBy(row => row.Name, StringComparer.Ordinal),
        };
    }

    public static bool MatchesFilter(CollectionRowViewModel row, CollectionFilter filter)
    {
        return filter switch
        {
            CollectionFilter.Units => row.Type == nameof(CardType.Unit),
            CollectionFilter.Spells => row.Type == nameof(CardType.Spell),
            CollectionFilter.Counters => row.Type == nameof(CardType.Counter),
            CollectionFilter.Champions => row.Type == nameof(CardType.Champion),
            CollectionFilter.OwnedOnly => row.OwnedCount > 0,
            _ => true,
        };
    }
}

public sealed record CollectionViewModel(
    CollectionFilter Filter,
    CollectionSort Sort,
    int SelectedIndex,
    IReadOnlyList<CollectionRowViewModel> Rows,
    CollectionRowViewModel? Detail);

public sealed record CollectionRowViewModel(
    string CardId,
    string Name,
    string Type,
    string Rarity,
    int OwnedCount,
    int UsedInDeckCount,
    string RulesText,
    string StatsText);
