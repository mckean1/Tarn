namespace Tarn.Domain;

public static class TarnFixtures
{
    public static DeckDefinition BuildDeck(string championId, params string[] mainDeckIds)
    {
        var champion = (ChampionCardDefinition)TarnCardRegistry.Get(championId);
        var cards = mainDeckIds.Select(TarnCardRegistry.Get).ToList();
        var fillerPool = TarnCardRegistry.NonChampionPool.ToList();
        var fillerIndex = 0;

        while (cards.Count < 30)
        {
            var next = fillerPool[fillerIndex % fillerPool.Count];
            if (cards.Count(existing => string.Equals(existing.Id, next.Id, StringComparison.Ordinal)) < 3)
            {
                cards.Add(next);
            }

            fillerIndex++;
        }

        return new DeckDefinition(champion, cards.Take(30).ToList());
    }

    public static MatchSetup BuildSetup(int seed, string championOne, IEnumerable<string> deckOne, string championTwo, IEnumerable<string> deckTwo, bool shuffleDecks = false)
    {
        return new MatchSetup
        {
            Seed = seed,
            PlayerOneDeck = BuildDeck(championOne, deckOne.ToArray()),
            PlayerTwoDeck = BuildDeck(championTwo, deckTwo.ToArray()),
            ShuffleDecks = shuffleDecks,
        };
    }
}
