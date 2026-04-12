using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public sealed class DeckQueries
{
    public DeckViewModel Build(World world, string humanPlayerId, int selectedIndex)
    {
        var player = world.Players[humanPlayerId];
        if (player.ActiveDeck is null)
        {
            return new DeckViewModel("[INVALID: NO ACTIVE DECK]", "0/31 cards", "Power 0/100", "Units 0 | Spells 0 | Counters 0", "None", [], null, 0);
        }

        var validation = DeckValidator.ValidateSubmittedDeck(world, player, player.ActiveDeck);
        var championCard = world.GetLatestDefinition(player.Collection.First(card => card.InstanceId == player.ActiveDeck.ChampionInstanceId).CardId);
        var nonChampions = player.ActiveDeck.NonChampionInstanceIds
            .Select(id => world.GetLatestDefinition(player.Collection.First(card => card.InstanceId == id).CardId))
            .ToList();
        var entries = new List<DeckEntryViewModel>
        {
            new("Champion", championCard.Name, championCard.Type.ToString(), championCard.Rarity.ToString(), championCard.RulesText, championCard.Attack, championCard.Health, championCard.Speed),
        };
        entries.AddRange(GroupedEntries("Units", nonChampions.Where(card => card.Type == CardType.Unit)));
        entries.AddRange(GroupedEntries("Spells", nonChampions.Where(card => card.Type == CardType.Spell)));
        entries.AddRange(GroupedEntries("Counters", nonChampions.Where(card => card.Type == CardType.Counter)));
        var clampedIndex = entries.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, entries.Count - 1);
        return new DeckViewModel(
            BuildLegalitySummary(validation),
            $"{player.ActiveDeck.NonChampionInstanceIds.Count + 1}/{world.Config.Season.DeckSize} cards",
            $"Power {nonChampions.Sum(card => card.Power) + championCard.Power}/{world.Config.Season.MaxDeckPower}",
            $"Units {nonChampions.Count(card => card.Type == CardType.Unit)} | Spells {nonChampions.Count(card => card.Type == CardType.Spell)} | Counters {nonChampions.Count(card => card.Type == CardType.Counter)}",
            championCard.Name,
            entries,
            entries.Count == 0 ? null : entries[clampedIndex],
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

    public static IReadOnlyList<DeckEntryViewModel> GroupedEntries(string group, IEnumerable<CardDefinition> cards)
    {
        return cards
            .OrderBy(card => card.Name, StringComparer.Ordinal)
            .Select(card => new DeckEntryViewModel(group, card.Name, card.Type.ToString(), card.Rarity.ToString(), card.RulesText, card.Attack, card.Health, card.Speed))
            .ToList();
    }
}

public sealed record DeckViewModel(
    string LegalitySummary,
    string TotalCards,
    string PowerSummary,
    string TypeSummary,
    string ChampionName,
    IReadOnlyList<DeckEntryViewModel> Entries,
    DeckEntryViewModel? Detail,
    int SelectedIndex);

public sealed record DeckEntryViewModel(
    string Group,
    string Name,
    string Type,
    string Rarity,
    string RulesText,
    int Attack,
    int Health,
    int Speed);
