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
            new("UN001", "Iron Squire", Attack: 1, Health: 3, HasDefender: true, Traits: ["Soldier"]),
            new("UN002", "Bastion Hound", Attack: 2, Health: 4, HasDefender: true, Traits: ["Beast"]),
            new("UN003", "Shield Acolyte", Attack: 1, Health: 2, Traits: ["Cleric"]),
            new("UN004", "Stoneframe Guard", Attack: 1, Health: 5, HasDefender: true, Traits: ["Construct"]),
            new("UN005", "Coil Sprite", Attack: 2, Health: 2, HasMagnet: true, Traits: ["Spirit"]),
            new("UN006", "Lure Beetle", Attack: 1, Health: 4, HasMagnet: true, Traits: ["Beast"]),
            new("UN007", "Static Adept", Attack: 2, Health: 3, Traits: ["Mystic"]),
            new("UN008", "Mirror Lancer", Attack: 3, Health: 2, HasMagnet: true, Traits: ["Knight"]),
            new("UN009", "Grave Tender", Attack: 2, Health: 1, Traits: ["Undead"]),
            new("UN010", "Ash Drifter", Attack: 3, Health: 1, Traits: ["Spirit"]),
            new("UN011", "Bone Clerk", Attack: 1, Health: 2, Traits: ["Undead"]),
            new("UN012", "Relic Rat", Attack: 2, Health: 2, Traits: ["Beast"]),
            new("UN013", "Banner Runner", Attack: 2, Health: 2, Traits: ["Soldier"]),
            new("UN014", "Hearthblade Initiate", Attack: 3, Health: 2, Traits: ["Duelist"]),
            new("UN015", "Bronze Turtle", Attack: 1, Health: 5, Traits: ["Beast"]),
            new("UN016", "Emberfoot Scout", Attack: 3, Health: 1, Traits: ["Scout"]),
            new("UN017", "Marching Clerk", Attack: 2, Health: 3, Traits: ["Citizen"]),
            new("UN018", "Field Surgeon", Attack: 1, Health: 3, Traits: ["Cleric"]),
            new("UN019", "Wardscribe Page", Attack: 1, Health: 3, Traits: ["Scholar"]),
            new("UN020", "Spellscar Witness", Attack: 2, Health: 3, Traits: ["Mystic"]),
        }.ToDictionary(card => card.Id, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, SpellCardDefinition> Spells =
        new List<SpellCardDefinition>
        {
            new("SP001", "Searing Order", ["Arcane"]),
            new("SP002", "Ruin Volley", ["Arcane"]),
            new("SP003", "Rallying Script", ["Tactic"]),
            new("SP004", "Fortify Line", ["Tactic"]),
            new("SP005", "Guided Coil", ["Arcane"]),
            new("SP006", "Mending Light", ["Holy"]),
        }.ToDictionary(card => card.Id, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, CounterCardDefinition> Counters =
        new List<CounterCardDefinition>
        {
            new("CT001", "Snap Denial", CounterTriggerType.EnemySpellWouldResolve, ["Arcane"]),
            new("CT002", "Quiet Refusal", CounterTriggerType.EnemyAbilityWouldResolve, ["Arcane"]),
            new("CT003", "Chain Break", CounterTriggerType.EnemyCounterWouldResolve, ["Arcane"]),
            new("CT004", "Brace the Line", CounterTriggerType.EnemyUnitAttacks, ["Tactic"]),
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
