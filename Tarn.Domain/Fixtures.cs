namespace Tarn.Domain;

public static class TarnFixtures
{
    public static ChampionDefinition GenericChampion(
        string id,
        int attack = 0,
        int health = 20,
        Keyword keywords = Keyword.None) =>
        new(id, Power: 0, BaseAttack: attack, BaseHealth: health, Keywords: keywords);

    public static UnitDefinition GenericUnit(
        string id,
        int attack = 0,
        int health = 1,
        Keyword keywords = Keyword.None,
        IReadOnlyList<EffectDefinition>? onPlayed = null,
        IReadOnlyList<EffectDefinition>? lastWish = null) =>
        new(id, Power: 1, BaseAttack: attack, BaseHealth: health, Keywords: keywords, OnPlayedEffects: onPlayed, LastWishEffects: lastWish);

    public static SpellDefinition GenericSpell(
        string id,
        Keyword keywords = Keyword.None,
        IReadOnlyList<EffectDefinition>? onPlayed = null) =>
        new(id, Power: 1, Keywords: keywords, OnPlayedEffects: onPlayed);

    public static CounterDefinition GenericCounter(
        string id,
        TriggerEventType trigger,
        IReadOnlyList<EffectDefinition>? onTrigger = null) =>
        new(id, Power: 1, new TriggerDefinition(trigger), OnPlayedEffects: onTrigger);

    public static DeckDefinition BuildDeck(ChampionDefinition champion, params CardDefinition[] cards)
    {
        var deckCards = cards.ToList();

        while (deckCards.Count < 30)
        {
            deckCards.Add(GenericSpell($"filler-{deckCards.Count + 1}"));
        }

        return new DeckDefinition(champion, deckCards.Take(30).ToList());
    }
}
