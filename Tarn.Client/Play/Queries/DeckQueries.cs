using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class DeckQueries
{
    public DeckViewModel Build(World world, string humanPlayerId, int selectedIndex)
    {
        var player = world.Players[humanPlayerId];
        var ownedCounts = player.Collection
            .GroupBy(card => card.CardId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        if (player.ActiveDeck is null)
        {
            return new DeckViewModel(
                "[INVALID: NO ACTIVE DECK]",
                $"0/{world.Config.Season.DeckSize} cards",
                $"Power 0/{world.Config.Season.MaxDeckPower}",
                "Units 0 · Spells 0 · Counters 0",
                "None",
                BuildEmptyGroups(),
                [],
                null,
                0);
        }

        var validation = DeckValidator.ValidateSubmittedDeck(world, player, player.ActiveDeck);
        var championCard = world.GetLatestDefinition(player.Collection.First(card => card.InstanceId == player.ActiveDeck.ChampionInstanceId).CardId);
        var nonChampions = player.ActiveDeck.NonChampionInstanceIds
            .Select(id => world.GetLatestDefinition(player.Collection.First(card => card.InstanceId == id).CardId))
            .ToList();

        var groups = new List<DeckGroupViewModel>
        {
            BuildGroup("Champion", [championCard], ownedCounts, "No champion selected."),
            BuildGroup("Units", nonChampions.Where(card => card.Type == CardType.Unit), ownedCounts, "No units in deck."),
            BuildGroup("Spells", nonChampions.Where(card => card.Type == CardType.Spell), ownedCounts, "No spells in deck."),
            BuildGroup("Counters", nonChampions.Where(card => card.Type == CardType.Counter), ownedCounts, "No counters in deck."),
        };

        var entries = groups.SelectMany(group => group.Entries).ToList();
        var clampedIndex = entries.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, entries.Count - 1);
        return new DeckViewModel(
            BuildLegalitySummary(validation),
            $"{player.ActiveDeck.NonChampionInstanceIds.Count + 1}/{world.Config.Season.DeckSize} cards",
            $"Power {nonChampions.Sum(card => card.Power) + championCard.Power}/{world.Config.Season.MaxDeckPower}",
            $"Units {nonChampions.Count(card => card.Type == CardType.Unit)} · Spells {nonChampions.Count(card => card.Type == CardType.Spell)} · Counters {nonChampions.Count(card => card.Type == CardType.Counter)}",
            championCard.Name,
            groups,
            entries,
            entries.Count == 0 ? null : BuildDetail(entries[clampedIndex]),
            clampedIndex);
    }

    public static string BuildLegalitySummary(DeckValidationResult validation)
    {
        if (validation.IsValid)
        {
            return "[LEGAL]";
        }

        var first = validation.Errors[0];
        if (first.Contains("30 non-Champion", StringComparison.OrdinalIgnoreCase))
        {
            return "[INVALID: 29/30 NON-CHAMPIONS]";
        }

        if (first.Contains("Power limit", StringComparison.OrdinalIgnoreCase))
        {
            return "[INVALID: POWER 104/100]";
        }

        if (first.Contains("copy limit", StringComparison.OrdinalIgnoreCase))
        {
            return "[INVALID: TOO MANY COPIES]";
        }

        return $"[INVALID: {first.ToUpperInvariant()}]";
    }

    public static IReadOnlyList<DeckEntryViewModel> GroupedEntries(string group, IEnumerable<CardDefinition> cards, IReadOnlyDictionary<string, int>? ownedCounts = null)
    {
        return cards
            .GroupBy(card => card.Id, StringComparer.Ordinal)
            .OrderBy(grouping => grouping.First().Name, StringComparer.Ordinal)
            .Select(grouping => BuildEntry(group, grouping.First(), grouping.Count(), ownedCounts?.GetValueOrDefault(grouping.Key) ?? grouping.Count()))
            .ToList();
    }

    private static IReadOnlyList<DeckGroupViewModel> BuildEmptyGroups()
    {
        return
        [
            new DeckGroupViewModel("Champion", [], "No champion selected."),
            new DeckGroupViewModel("Units", [], "No units in deck."),
            new DeckGroupViewModel("Spells", [], "No spells in deck."),
            new DeckGroupViewModel("Counters", [], "No counters in deck."),
        ];
    }

    private static DeckGroupViewModel BuildGroup(
        string name,
        IEnumerable<CardDefinition> cards,
        IReadOnlyDictionary<string, int> ownedCounts,
        string emptyState)
    {
        var entries = GroupedEntries(name, cards, ownedCounts);
        return new DeckGroupViewModel(name, entries, emptyState);
    }

    private static DeckEntryViewModel BuildEntry(string group, CardDefinition card, int copiesInDeck, int ownedCount)
    {
        return new DeckEntryViewModel(
            group,
            card.Id,
            card.Name,
            card.Type.ToString(),
            card.Rarity.ToString(),
            string.IsNullOrWhiteSpace(card.RulesText) ? "No rules text." : card.RulesText,
            CardTextFormatter.BuildCollectionStats(card),
            CardTextFormatter.BuildKeywordSummary(card.Keywords),
            copiesInDeck,
            ownedCount);
    }

    private static DeckDetailViewModel BuildDetail(DeckEntryViewModel entry)
        => new(
            entry.Name,
            entry.Type,
            entry.Rarity,
            entry.StatsText,
            entry.RulesText,
            entry.KeywordsText,
            entry.CopiesInDeck,
            entry.OwnedCount);
}

public sealed record DeckViewModel(
    string LegalitySummary,
    string TotalCards,
    string PowerSummary,
    string TypeSummary,
    string ChampionName,
    IReadOnlyList<DeckGroupViewModel> Groups,
    IReadOnlyList<DeckEntryViewModel> Entries,
    DeckDetailViewModel? Detail,
    int SelectedIndex);

public sealed record DeckGroupViewModel(
    string Name,
    IReadOnlyList<DeckEntryViewModel> Entries,
    string EmptyState);

public sealed record DeckEntryViewModel(
    string Group,
    string CardId,
    string Name,
    string Type,
    string Rarity,
    string RulesText,
    string? StatsText,
    string KeywordsText,
    int CopiesInDeck,
    int OwnedCount);

public sealed record DeckDetailViewModel(
    string Name,
    string Type,
    string Rarity,
    string? StatsText,
    string RulesText,
    string KeywordsText,
    int CopiesInDeck,
    int OwnedCount);
