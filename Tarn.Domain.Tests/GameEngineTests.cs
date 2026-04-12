using Tarn.Domain;

namespace Tarn.Domain.Tests;

public sealed class GameEngineTests
{
    [Fact]
    public void DeckValidation_IgnoresChampionPowerForMainDeckCap()
    {
        var champion = new ChampionDefinition("high-power-champion", Power: 90, BaseAttack: 0, BaseHealth: 20);
        var mainDeck = Enumerable.Range(1, 30)
            .Select(index => (CardDefinition)new SpellDefinition($"spell-{index}", Power: 3))
            .ToList();
        var deck = new DeckDefinition(champion, mainDeck);

        deck.Validate();

        Assert.Equal(90, deck.MainDeckPower);
        Assert.Equal(180, deck.TotalPower);
    }

    [Fact]
    public void DeckValidation_FailsWhenMainDeckPowerExceedsCap()
    {
        var champion = TarnFixtures.GenericChampion("champion");
        var mainDeck = Enumerable.Range(1, 30)
            .Select(index => (CardDefinition)new SpellDefinition($"spell-{index}", Power: 4))
            .ToList();
        var deck = new DeckDefinition(champion, mainDeck);

        var action = () => deck.Validate();

        Assert.Throws<InvalidOperationException>(action);
        Assert.Equal(120, deck.MainDeckPower);
    }

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
    public void SimultaneousBatch_DoesNotTargetUnitThatEnteredFromSameBatch()
    {
        var engine = new GameEngine();
        var snipe = TarnFixtures.GenericSpell(
            "snipe",
            onPlayed:
            [
                new EffectDefinition(EffectType.Damage, TargetSelector.AutoEnemyUnit, Amount: 2),
            ]);
        var entrant = TarnFixtures.GenericUnit("entrant", attack: 0, health: 2);

        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-a"), snipe),
            playerTwoId: "p2",
            playerTwoDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-b"), entrant),
            seed: 31,
            gameNumber: 1);

        game.PlayerOne.Library = new Queue<CardDefinition>([snipe]);
        game.PlayerTwo.Library = new Queue<CardDefinition>([entrant]);

        engine.PlayRound(game);

        var survivingUnit = Assert.Single(game.PlayerTwo.Board);
        Assert.Equal("entrant", survivingUnit.Definition.Id);
        Assert.Equal(2, survivingUnit.CurrentHealth);
        Assert.DoesNotContain(game.Logs, entry => entry.Message.Contains("takes 2 damage", StringComparison.Ordinal));
    }

    [Fact]
    public void SimultaneousBatch_UsesPreBatchBoardForAutomaticTargeting()
    {
        var engine = new GameEngine();
        var snipe = TarnFixtures.GenericSpell(
            "snipe",
            onPlayed:
            [
                new EffectDefinition(EffectType.Damage, TargetSelector.AutoEnemyUnit, Amount: 2),
            ]);
        var oldUnitDefinition = TarnFixtures.GenericUnit("old-unit", attack: 0, health: 2);
        var entrant = TarnFixtures.GenericUnit("entrant", attack: 0, health: 2);

        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-a"), snipe),
            playerTwoId: "p2",
            playerTwoDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-b"), entrant),
            seed: 37,
            gameNumber: 1);

        game.PlayerTwo.Board.Add(new CombatCardState
        {
            InstanceId = "p2-old",
            OwnerId = "p2",
            Definition = oldUnitDefinition,
            EnteredRound = 0,
            PlayOrder = 1,
            CurrentHealth = 2,
        });
        game.PlayerOne.Library = new Queue<CardDefinition>([snipe]);
        game.PlayerTwo.Library = new Queue<CardDefinition>([entrant]);

        engine.PlayRound(game);

        var survivingUnit = Assert.Single(game.PlayerTwo.Board);
        Assert.Equal("entrant", survivingUnit.Definition.Id);
        Assert.Equal(2, survivingUnit.CurrentHealth);
        Assert.Contains(game.Logs, entry => entry.Message.Contains("p2-old takes 2 damage", StringComparison.Ordinal));
    }

    [Fact]
    public void QuickBatch_IsStillSimultaneousWithinTheQuickStep()
    {
        var engine = new GameEngine();
        var quickSnipe = TarnFixtures.GenericSpell(
            "quick-snipe",
            keywords: Keyword.Quick,
            onPlayed:
            [
                new EffectDefinition(EffectType.Damage, TargetSelector.AutoEnemyUnit, Amount: 2),
            ]);
        var quickEntrant = TarnFixtures.GenericUnit("quick-entrant", attack: 0, health: 2, keywords: Keyword.Quick);

        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-a"), quickSnipe),
            playerTwoId: "p2",
            playerTwoDeck: TarnFixtures.BuildDeck(TarnFixtures.GenericChampion("champion-b"), quickEntrant),
            seed: 41,
            gameNumber: 1);

        game.PlayerOne.Library = new Queue<CardDefinition>([quickSnipe]);
        game.PlayerTwo.Library = new Queue<CardDefinition>([quickEntrant]);

        engine.PlayRound(game);

        var survivingUnit = Assert.Single(game.PlayerTwo.Board);
        Assert.Equal("quick-entrant", survivingUnit.Definition.Id);
        Assert.Equal(2, survivingUnit.CurrentHealth);
        Assert.DoesNotContain(game.Logs, entry => entry.Message.Contains("quick-entrant") && entry.Message.Contains("takes 2 damage", StringComparison.Ordinal));
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
    public void PlayGame_AllowsGamesToContinuePastTwoHundredRounds()
    {
        var engine = new GameEngine();
        var playerOneDeck = TarnFixtures.BuildDeck(new ChampionDefinition("champion-a", Power: 0, BaseAttack: 1, BaseHealth: 25000));
        var playerTwoDeck = TarnFixtures.BuildDeck(new ChampionDefinition("champion-b", Power: 0, BaseAttack: 0, BaseHealth: 25000));

        var game = engine.CreateGame(
            higherSeedPlayerId: "p1",
            playerOneId: "p1",
            playerOneDeck: playerOneDeck,
            playerTwoId: "p2",
            playerTwoDeck: playerTwoDeck,
            seed: 43,
            gameNumber: 1);

        var winner = engine.PlayGame(game);

        Assert.Equal("p1", winner);
        Assert.True(game.RoundNumber > 200);
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
