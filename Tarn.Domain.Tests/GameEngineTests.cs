using Tarn.Domain;

namespace Tarn.Domain.Tests;

public sealed class GameEngineTests
{
    [Fact]
    public void QuickCardResolvesBeforeNormalCard()
    {
        var engine = new GameEngine();
        var fastSpell = TarnFixtures.GenericSpell(
            "quick-spell",
            keywords: Keyword.Quick,
            onPlayed:
            [
                new EffectDefinition(EffectType.Damage, TargetSelector.EnemyChampion, Amount: 2),
            ]);
        var slowSpell = TarnFixtures.GenericSpell(
            "slow-spell",
            onPlayed:
            [
                new EffectDefinition(EffectType.Damage, TargetSelector.EnemyChampion, Amount: 1),
            ]);

        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-a"), fastSpell),
            playerTwoId: "p2",
            playerTwoDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-b"), slowSpell),
            seed: 7,
            gameNumber: 1);

        game.PlayerOne.Library = new Queue<CardDefinition>([fastSpell]);
        game.PlayerTwo.Library = new Queue<CardDefinition>([slowSpell]);

        engine.PlayRound(game);

        var quickIndex = game.Logs.FindIndex(entry => entry.Message.Contains("Spell quick-spell resolves", StringComparison.Ordinal));
        var slowIndex = game.Logs.FindIndex(entry => entry.Message.Contains("Spell slow-spell resolves", StringComparison.Ordinal));

        Assert.True(quickIndex >= 0 && slowIndex >= 0);
        Assert.True(quickIndex < slowIndex);
    }

    [Fact]
    public void UnitsPlayedThisRoundDoNotAttack()
    {
        var engine = new GameEngine();
        var attacker = TarnFixtures.GenericUnit("attacker", attack: 3, health: 2);

        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-a"), attacker),
            playerTwoId: "p2",
            playerTwoDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-b")),
            seed: 11,
            gameNumber: 1);

        game.PlayerOne.Library = new Queue<CardDefinition>([attacker]);
        game.PlayerTwo.Library = new Queue<CardDefinition>();

        engine.PlayRound(game);

        Assert.Equal(19, game.PlayerTwo.Champion.CurrentHealth);
        Assert.DoesNotContain(game.Logs, entry => entry.Message.Contains("attacks for 3", StringComparison.Ordinal));
    }

    [Fact]
    public void DefenderRedirectsDamageInPlayOrder()
    {
        var engine = new GameEngine();
        var defenderOne = TarnFixtures.GenericUnit("defender-1", attack: 0, health: 1, keywords: Keyword.Defender);
        var defenderTwo = TarnFixtures.GenericUnit("defender-2", attack: 0, health: 2, keywords: Keyword.Defender);
        var burn = TarnFixtures.GenericSpell(
            "burn",
            onPlayed:
            [
                new EffectDefinition(EffectType.Damage, TargetSelector.EnemyChampion, Amount: 4),
            ]);

        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-a"), burn),
            playerTwoId: "p2",
            playerTwoDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-b")),
            seed: 17,
            gameNumber: 1);

        game.PlayerTwo.Board.Add(new CombatCardState
        {
            InstanceId = "p2-d1",
            OwnerId = "p2",
            Definition = defenderOne,
            EnteredRound = 0,
            PlayOrder = 1,
            CurrentHealth = 1,
        });
        game.PlayerTwo.Board.Add(new CombatCardState
        {
            InstanceId = "p2-d2",
            OwnerId = "p2",
            Definition = defenderTwo,
            EnteredRound = 0,
            PlayOrder = 2,
            CurrentHealth = 2,
        });
        game.PlayerOne.Library = new Queue<CardDefinition>([burn]);
        game.PlayerTwo.Library = new Queue<CardDefinition>([TarnFixtures.GenericSpell("blank")]);

        engine.PlayRound(game);

        Assert.Equal(19, game.PlayerTwo.Champion.CurrentHealth);
        Assert.Empty(game.PlayerTwo.Board);
        Assert.Contains(game.Logs, entry => entry.Message.Contains("redirected to Defender p2-d1", StringComparison.Ordinal));
        Assert.Contains(game.Logs, entry => entry.Message.Contains("redirected to Defender p2-d2", StringComparison.Ordinal));
    }

    [Fact]
    public void CounterOnlyTriggersOnFutureEventsAfterItIsSet()
    {
        var engine = new GameEngine();
        var counter = TarnFixtures.GenericCounter(
            "future-counter",
            TriggerEventType.ChampionDamaged,
            onTrigger:
            [
                new EffectDefinition(EffectType.Damage, TargetSelector.EnemyChampion, Amount: 1),
            ]);
        var burn = TarnFixtures.GenericSpell(
            "burn",
            onPlayed:
            [
                new EffectDefinition(EffectType.Damage, TargetSelector.EnemyChampion, Amount: 1),
            ]);

        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-a"), counter, burn),
            playerTwoId: "p2",
            playerTwoDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-b")),
            seed: 19,
            gameNumber: 1);

        game.PlayerOne.Library = new Queue<CardDefinition>([counter, burn]);
        game.PlayerTwo.Library = new Queue<CardDefinition>([TarnFixtures.GenericSpell("blank-1"), TarnFixtures.GenericSpell("blank-2")]);

        engine.PlayRound(game);
        Assert.Equal(20, game.PlayerTwo.Champion.CurrentHealth);

        engine.PlayRound(game);
        Assert.Equal(18, game.PlayerTwo.Champion.CurrentHealth);
        Assert.Contains(game.Logs, entry => entry.Message.Contains("Counter future-counter triggers", StringComparison.Ordinal));
    }

    [Fact]
    public void FatigueIncreasesByOnePerInstance()
    {
        var engine = new GameEngine();
        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-a")),
            playerTwoId: "p2",
            playerTwoDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-b")),
            seed: 23,
            gameNumber: 1);

        game.PlayerOne.Library.Clear();
        game.PlayerTwo.Library.Clear();

        engine.PlayRound(game);
        engine.PlayRound(game);

        Assert.Equal(17, game.PlayerOne.Champion.CurrentHealth);
        Assert.Equal(2, game.PlayerOne.FatigueCount);
    }

    [Fact]
    public void MatchScoringFollowsLockedLeagueRules()
    {
        var match = new MatchResult
        {
            WinnerPlayerId = "p1",
            LoserPlayerId = "p2",
            WinnerGameWins = 2,
            LoserGameWins = 1,
            Games =
            [
                new GameResult(1, 0, "p1", "p2"),
                new GameResult(1, 0, "p2", "p1"),
                new GameResult(1, 0, "p1", "p2"),
            ],
        };

        Assert.Equal(2, match.WinnerMatchPoints);
        Assert.Equal(1, match.LoserMatchPoints);
    }
}
