namespace Tarn.Domain;

[Flags]
public enum Keyword
{
    None = 0,
    Quick = 1 << 0,
    Taunt = 1 << 1,
    Defender = 1 << 2,
    Rally = 1 << 3,
    LastWish = 1 << 4,
    Regen = 1 << 5,
}

public enum CardType
{
    Champion,
    Unit,
    Spell,
    Counter,
}

public enum EffectType
{
    Damage,
    Heal,
    GrantAttackThisRound,
    PreventAttacksThisRound,
}

public enum TargetSelector
{
    None,
    Source,
    AlliedUnitsWithAttack,
    EnemyChampion,
    AutoEnemyUnit,
}

public enum TriggerEventType
{
    CardPlayed,
    CardResolved,
    UnitEnteredPlay,
    UnitDestroyed,
    ChampionDamaged,
    RoundEnded,
}

public sealed record EffectDefinition(
    EffectType Type,
    TargetSelector Selector,
    int Amount = 0,
    bool AllowChampionTarget = false);

public sealed record TriggerDefinition(
    TriggerEventType EventType,
    bool AllowSelfTrigger = false);

public abstract record CardDefinition(
    string Id,
    CardType Type,
    int Power,
    Keyword Keywords)
{
    public virtual int Attack => 0;
    public virtual int Health => 0;
    public virtual IReadOnlyList<EffectDefinition> OnPlayedEffects => [];
    public virtual IReadOnlyList<EffectDefinition> LastWishEffects => [];
}

public sealed record ChampionDefinition(
    string Id,
    int Power,
    int BaseAttack,
    int BaseHealth,
    Keyword Keywords = Keyword.None,
    IReadOnlyList<EffectDefinition>? OnPlayedEffects = null)
    : CardDefinition(Id, CardType.Champion, Power, Keywords)
{
    public override int Attack => BaseAttack;
    public override int Health => BaseHealth;
    public override IReadOnlyList<EffectDefinition> OnPlayedEffects { get; } = OnPlayedEffects ?? [];
}

public sealed record UnitDefinition(
    string Id,
    int Power,
    int BaseAttack,
    int BaseHealth,
    Keyword Keywords = Keyword.None,
    IReadOnlyList<EffectDefinition>? OnPlayedEffects = null,
    IReadOnlyList<EffectDefinition>? LastWishEffects = null)
    : CardDefinition(Id, CardType.Unit, Power, Keywords)
{
    public override int Attack => BaseAttack;
    public override int Health => BaseHealth;
    public override IReadOnlyList<EffectDefinition> OnPlayedEffects { get; } = OnPlayedEffects ?? [];
    public override IReadOnlyList<EffectDefinition> LastWishEffects { get; } = LastWishEffects ?? [];
}

public sealed record SpellDefinition(
    string Id,
    int Power,
    Keyword Keywords = Keyword.None,
    IReadOnlyList<EffectDefinition>? OnPlayedEffects = null)
    : CardDefinition(Id, CardType.Spell, Power, Keywords)
{
    public override IReadOnlyList<EffectDefinition> OnPlayedEffects { get; } = OnPlayedEffects ?? [];
}

public sealed record CounterDefinition(
    string Id,
    int Power,
    TriggerDefinition Trigger,
    Keyword Keywords = Keyword.None,
    IReadOnlyList<EffectDefinition>? OnPlayedEffects = null)
    : CardDefinition(Id, CardType.Counter, Power, Keywords)
{
    public TriggerDefinition Trigger { get; } = Trigger;
    public override IReadOnlyList<EffectDefinition> OnPlayedEffects { get; } = OnPlayedEffects ?? [];
}

public sealed class DeckDefinition
{
    public DeckDefinition(ChampionDefinition champion, IReadOnlyList<CardDefinition> mainDeck)
    {
        Champion = champion;
        MainDeck = mainDeck;
    }

    public ChampionDefinition Champion { get; }
    public IReadOnlyList<CardDefinition> MainDeck { get; }

    public int MainDeckPower => MainDeck.Sum(card => card.Power);
    public int TotalPower => Champion.Power + MainDeckPower;

    public void Validate()
    {
        if (MainDeck.Count != 30)
        {
            throw new InvalidOperationException("Each Tarn deck must contain exactly 30 non-Champion cards.");
        }

        if (MainDeck.Any(card => card.Type == CardType.Champion))
        {
            throw new InvalidOperationException("Champion cards cannot appear in the 30-card main deck.");
        }

        var overLimit = MainDeck
            .GroupBy(card => card.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 3);

        if (overLimit is not null)
        {
            throw new InvalidOperationException($"Card '{overLimit.Key}' exceeds the three-copy limit.");
        }

        if (MainDeckPower > 100)
        {
            throw new InvalidOperationException("Deck exceeds the permanent 100 power cap.");
        }
    }
}
