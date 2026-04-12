using Tarn.Domain;

var engine = new GameEngine();

var sharedChampion = TarnFixtures.GenericChampion("champion-shell", attack: 1, health: 12);
var openingUnit = TarnFixtures.GenericUnit("unit-shell", attack: 2, health: 3, keywords: Keyword.Regen);
var openingSpell = TarnFixtures.GenericSpell(
    "spell-shell",
    onPlayed:
    [
        new EffectDefinition(EffectType.Damage, TargetSelector.EnemyChampion, Amount: 1),
    ]);
var reactiveCounter = TarnFixtures.GenericCounter(
    "counter-shell",
    TriggerEventType.ChampionDamaged,
    onTrigger:
    [
        new EffectDefinition(EffectType.Damage, TargetSelector.EnemyChampion, Amount: 1),
    ]);

var firstDeck = TarnFixtures.BuildDeck(sharedChampion, openingUnit, openingSpell, reactiveCounter);
var secondDeck = TarnFixtures.BuildDeck(sharedChampion, openingUnit, openingSpell, reactiveCounter);

var game = engine.CreateGame(
    higherSeedPlayerId: "alpha",
    playerOneId: "alpha",
    playerOneDeck: firstDeck,
    playerTwoId: "beta",
    playerTwoDeck: secondDeck,
    seed: 42,
    gameNumber: 1);

engine.PlayRound(game);

Console.WriteLine("Tarn engine scaffold ready.");
Console.WriteLine($"Round: {game.RoundNumber}");
Console.WriteLine($"Alpha Champion: {game.PlayerOne.Champion.CurrentHealth}");
Console.WriteLine($"Beta Champion: {game.PlayerTwo.Champion.CurrentHealth}");
Console.WriteLine("Recent log:");
foreach (var entry in game.Logs.TakeLast(8))
{
    Console.WriteLine($"[{entry.Sequence}] {entry.Step}: {entry.Message}");
}
