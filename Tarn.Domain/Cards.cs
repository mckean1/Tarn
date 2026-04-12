using System.Text.Json.Serialization;

namespace Tarn.Domain;

public enum CardType
{
    Champion,
    Unit,
    Spell,
    Counter,
}

public enum TriggerType
{
    StartOfRound,
    OnPlay,
    OnAttack,
    OnDamage,
    OnSurvive,
    OnDeath,
    OnDestroyed,
    OnCountered,
    EndOfRound,
}

public enum CounterTriggerType
{
    EnemySpellWouldResolve,
    EnemyAbilityWouldResolve,
    EnemyCounterWouldResolve,
    EnemyUnitAttacks,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$cardKind")]
[JsonDerivedType(typeof(ChampionCardDefinition), "champion")]
[JsonDerivedType(typeof(UnitCardDefinition), "unit")]
[JsonDerivedType(typeof(SpellCardDefinition), "spell")]
[JsonDerivedType(typeof(CounterCardDefinition), "counter")]
public abstract record CardDefinition(
    string Id,
    string Name,
    CardType Type,
    int Power,
    int Attack,
    int Health,
    int Speed,
    bool HasDefender = false,
    bool HasMagnet = false,
    bool CanAttack = false,
    IReadOnlyList<string>? Traits = null)
{
    public IReadOnlyList<string> Traits { get; init; } = Traits ?? [];
    public CardRarity Rarity { get; init; } = CardRarity.Common;
    public string SetId { get; init; } = string.Empty;
    public int Version { get; init; } = 1;
    public bool IsUnique { get; init; }
    public string RulesText { get; init; } = string.Empty;
    public IReadOnlyList<string> Keywords { get; init; } = [];
    public int EffectValue { get; init; }
    public TriggerType? TriggerTiming { get; init; }
    public CounterTriggerType? CounterWindow { get; init; }
    public bool OncePerRound { get; init; }
}

public sealed record ChampionCardDefinition(
    string Id,
    string Name,
    int Speed,
    int Attack = 0,
    int Health = 20,
    bool CanAttack = false,
    IReadOnlyList<string>? Traits = null)
    : CardDefinition(Id, Name, CardType.Champion, Power: 0, Attack, Health, Speed, false, false, CanAttack, Traits);

public sealed record UnitCardDefinition(
    string Id,
    string Name,
    int Attack,
    int Health,
    bool HasDefender = false,
    bool HasMagnet = false,
    IReadOnlyList<string>? Traits = null)
    : CardDefinition(Id, Name, CardType.Unit, Power: 1, Attack, Health, Speed: 0, HasDefender, HasMagnet, false, Traits);

public sealed record SpellCardDefinition(
    string Id,
    string Name,
    IReadOnlyList<string>? Traits = null)
    : CardDefinition(Id, Name, CardType.Spell, Power: 1, Attack: 0, Health: 0, Speed: 0, false, false, false, Traits);

public sealed record CounterCardDefinition(
    string Id,
    string Name,
    CounterTriggerType Trigger,
    IReadOnlyList<string>? Traits = null)
    : CardDefinition(Id, Name, CardType.Counter, Power: 1, Attack: 0, Health: 0, Speed: 0, false, false, false, Traits);

public sealed class DeckDefinition
{
    public DeckDefinition(ChampionCardDefinition champion, IReadOnlyList<CardDefinition> mainDeck)
    {
        Champion = champion;
        MainDeck = mainDeck;
    }

    public ChampionCardDefinition Champion { get; }
    public IReadOnlyList<CardDefinition> MainDeck { get; }

    public void Validate()
    {
        if (MainDeck.Count != 30)
        {
            throw new InvalidOperationException("Each Tarn deck must contain exactly 30 non-Champion cards.");
        }

        if (MainDeck.Any(card => card.Type == CardType.Champion))
        {
            throw new InvalidOperationException("Champion cards cannot appear in the main deck.");
        }

        var overLimit = MainDeck
            .GroupBy(card => card.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 3);

        if (overLimit is not null)
        {
            throw new InvalidOperationException($"Card '{overLimit.Key}' exceeds the three-copy limit.");
        }

        if (MainDeck.Sum(card => card.Power) > 100)
        {
            throw new InvalidOperationException("Deck exceeds the 100 Power cap.");
        }
    }
}

public sealed class DeckValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
}

public static class DeckValidator
{
    public static DeckValidationResult ValidateSubmittedDeck(World world, Player player, SubmittedDeck submittedDeck)
    {
        var result = new DeckValidationResult();
        var config = world.Config.Season;
        var availableCards = player.Collection
            .Where(card => !card.IsListed && !card.PendingSettlement)
            .ToDictionary(card => card.InstanceId, StringComparer.Ordinal);

        if (!availableCards.TryGetValue(submittedDeck.ChampionInstanceId, out var championCard))
        {
            result.Errors.Add("Champion card is not available in the player collection.");
            return result;
        }

        if (submittedDeck.NonChampionInstanceIds.Count != config.NonChampionCount)
        {
            result.Errors.Add($"Deck must contain exactly {config.NonChampionCount} non-Champion cards.");
        }

        if (submittedDeck.NonChampionInstanceIds.Distinct(StringComparer.Ordinal).Count() != submittedDeck.NonChampionInstanceIds.Count)
        {
            result.Errors.Add("Deck cannot contain duplicate card instances.");
        }

        if (submittedDeck.NonChampionInstanceIds.Any(instanceId => !availableCards.ContainsKey(instanceId)))
        {
            result.Errors.Add("Deck contains unavailable non-Champion cards.");
        }

        var championDefinition = world.GetLatestDefinition(championCard.CardId);
        if (championDefinition.Type != CardType.Champion)
        {
            result.Errors.Add("Submitted champion is not a Champion card.");
        }

        var nonChampionDefinitions = submittedDeck.NonChampionInstanceIds
            .Where(availableCards.ContainsKey)
            .Select(instanceId => world.GetLatestDefinition(availableCards[instanceId].CardId))
            .ToList();

        if (nonChampionDefinitions.Any(card => card.Type == CardType.Champion))
        {
            result.Errors.Add("Champion cards cannot appear among the 30 non-Champion cards.");
        }

        var illegalCopies = nonChampionDefinitions
            .GroupBy(card => card.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > config.MaxCopiesPerCard);
        if (illegalCopies is not null)
        {
            result.Errors.Add($"Card '{illegalCopies.Key}' exceeds the {config.MaxCopiesPerCard}-copy limit.");
        }

        var deckPower = nonChampionDefinitions.Sum(card => card.Power) + championDefinition.Power;
        if (deckPower > config.MaxDeckPower)
        {
            result.Errors.Add($"Deck exceeds the Power limit of {config.MaxDeckPower}.");
        }

        var standardSets = world.StandardSetIds.ToHashSet(StringComparer.Ordinal);
        if (!standardSets.Contains(championDefinition.SetId) || nonChampionDefinitions.Any(card => !standardSets.Contains(card.SetId)))
        {
            result.Errors.Add("Deck includes cards that are not Standard-legal.");
        }

        if (submittedDeck.NonChampionInstanceIds.Count + 1 != config.DeckSize)
        {
            result.Errors.Add($"Deck must contain exactly {config.DeckSize} total cards.");
        }

        return result;
    }
}

public static class TarnCardRegistry
{
    public static readonly IReadOnlyList<string> ChampionSpeedTiebreakOrder =
    [
        "CH001", "CH002", "CH003", "CH004", "CH005", "CH006", "CH007", "CH008", "CH009", "CH010",
        "CH011", "CH012", "CH013", "CH014", "CH015", "CH016", "CH017", "CH018", "CH019", "CH020",
    ];

    public static readonly IReadOnlyDictionary<string, ChampionCardDefinition> Champions =
        new List<ChampionCardDefinition>
        {
            new("CH001", "Veyn, Ember Edge", Speed: 10, Traits: ["Duelist"]),
            new("CH002", "Serah, Banner of Motion", Speed: 10, Traits: ["Marshal"]),
            new("CH003", "Garruk, Wall Marshal", Speed: 9, Traits: ["Soldier"]),
            new("CH004", "Lyra, Coil Herald", Speed: 9, Traits: ["Mystic"]),
            new("CH005", "Morcant, Grave Choir", Speed: 8, Traits: ["Undead"]),
            new("CH006", "Selka, Ash Collector", Speed: 8, Traits: ["Reaper"]),
            new("CH007", "Noct, Null Regent", Speed: 7, Traits: ["Arcane"]),
            new("CH008", "Aster, Chain Custodian", Speed: 7, Traits: ["Warden"]),
            new("CH009", "Toma, Bulwark Leech", Speed: 6, Traits: ["Guardian"]),
            new("CH010", "Kael, War Pulse", Speed: 6, Traits: ["Berserker"]),
            new("CH011", "Brin, Iron Survivor", Speed: 5, Traits: ["Veteran"]),
            new("CH012", "Sable, Echo Cage", Speed: 5, Traits: ["Void"]),
            new("CH013", "Edda, Last Hearth", Speed: 4, Traits: ["Keeper"]),
            new("CH014", "Irix, Spellwake Sage", Speed: 4, Traits: ["Arcane"]),
            new("CH015", "Dren, Skybreaker", Speed: 3, Attack: 1, CanAttack: true, Traits: ["Raider"]),
            new("CH016", "Nema, Quiet Mason", Speed: 3, Traits: ["Artisan"]),
            new("CH017", "Orren, Pale Warden", Speed: 2, Traits: ["Spirit"]),
            new("CH018", "Vale, Lean Season", Speed: 2, Traits: ["Oracle"]),
            new("CH019", "Hest, Glass Oath", Speed: 1, Traits: ["Sentinel"]),
            new("CH020", "Malvek, Ruin Script", Speed: 1, Traits: ["Scholar"]),
        }.ToDictionary(card => card.Id, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, UnitCardDefinition> Units =
        new List<UnitCardDefinition>
        {
            new("UN001", "Ashen Scout", Attack: 1, Health: 3, HasDefender: true, Traits: ["Soldier"]),
            new("UN002", "Grave Pike", Attack: 2, Health: 4, HasDefender: true, Traits: ["Beast"]),
            new("UN003", "Lantern Sentry", Attack: 1, Health: 2, Traits: ["Cleric"]),
            new("UN004", "Mire Raider", Attack: 1, Health: 5, HasDefender: true, Traits: ["Construct"]),
            new("UN005", "Iron Vagrant", Attack: 2, Health: 2, HasMagnet: true, Traits: ["Spirit"]),
            new("UN006", "Hollow Stalker", Attack: 1, Health: 4, HasMagnet: true, Traits: ["Beast"]),
            new("UN007", "Briar Duelist", Attack: 2, Health: 3, Traits: ["Mystic"]),
            new("UN008", "Cinder Wolf", Attack: 3, Health: 2, HasMagnet: true, Traits: ["Knight"]),
            new("UN009", "Marsh Hunter", Attack: 2, Health: 1, Traits: ["Undead"]),
            new("UN010", "Stoneblade Initiate", Attack: 3, Health: 1, Traits: ["Spirit"]),
            new("UN011", "Dusk Skirmisher", Attack: 1, Health: 2, Traits: ["Undead"]),
            new("UN012", "Ember Watcher", Attack: 2, Health: 2, Traits: ["Beast"]),
            new("UN013", "Rustshield Guard", Attack: 2, Health: 2, Traits: ["Soldier"]),
            new("UN014", "Nightfen Strider", Attack: 3, Health: 2, Traits: ["Duelist"]),
            new("UN015", "Thorn Pike Adept", Attack: 1, Health: 5, Traits: ["Beast"]),
            new("UN016", "Pale Banneret", Attack: 3, Health: 1, Traits: ["Scout"]),
            new("UN017", "Rook Outrider", Attack: 2, Health: 3, Traits: ["Citizen"]),
            new("UN018", "Soottrail Ranger", Attack: 1, Health: 3, Traits: ["Cleric"]),
            new("UN019", "Warden of Cinders", Attack: 1, Health: 3, Traits: ["Scholar"]),
            new("UN020", "Blackbriar Sentinel", Attack: 2, Health: 3, Traits: ["Mystic"]),
        }.ToDictionary(card => card.Id, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, SpellCardDefinition> Spells =
        new List<SpellCardDefinition>
        {
            new("SP001", "Iron Rite", ["Arcane"]),
            new("SP002", "Ember Sigil", ["Arcane"]),
            new("SP003", "Thorn Lash", ["Tactic"]),
            new("SP004", "Gravebind", ["Tactic"]),
            new("SP005", "Ashfall Burst", ["Arcane"]),
            new("SP006", "Lantern Flare", ["Holy"]),
        }.ToDictionary(card => card.Id, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, CounterCardDefinition> Counters =
        new List<CounterCardDefinition>
        {
            new("CT001", "Bastion Ward", CounterTriggerType.EnemySpellWouldResolve, ["Arcane"]),
            new("CT002", "Null Brand", CounterTriggerType.EnemyAbilityWouldResolve, ["Arcane"]),
            new("CT003", "Turn Aside", CounterTriggerType.EnemyCounterWouldResolve, ["Arcane"]),
            new("CT004", "Last Denial", CounterTriggerType.EnemyUnitAttacks, ["Tactic"]),
        }.ToDictionary(card => card.Id, StringComparer.Ordinal);

    public static readonly IReadOnlyList<CardDefinition> NonChampionPool =
        Units.Values.Cast<CardDefinition>()
            .Concat(Spells.Values)
            .Concat(Counters.Values)
            .OrderBy(card => card.Id, StringComparer.Ordinal)
            .ToList();

    public static CardDefinition Get(string id)
    {
        if (Champions.TryGetValue(id, out var champion))
        {
            return champion;
        }

        if (Units.TryGetValue(id, out var unit))
        {
            return unit;
        }

        if (Spells.TryGetValue(id, out var spell))
        {
            return spell;
        }

        if (Counters.TryGetValue(id, out var counter))
        {
            return counter;
        }

        throw new KeyNotFoundException($"Unknown Tarn card '{id}'.");
    }
}
