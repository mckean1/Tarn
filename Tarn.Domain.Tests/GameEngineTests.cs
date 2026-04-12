using Tarn.Domain;

namespace Tarn.Domain.Tests;

public sealed class GameEngineTests
{
    [Fact]
    public void SameSeedProducesSameReplay()
    {
        var engine = new GameEngine();

        var first = engine.RunRandomMatch(123);
        var second = engine.RunRandomMatch(123);

        Assert.Equal(first.WinnerPlayerId, second.WinnerPlayerId);
        Assert.Equal(first.ReplayText, second.ReplayText);
    }

    [Fact]
    public void UnitsDoNotAttackOnTheTurnTheyEnter()
    {
        var engine = new GameEngine();
        var state = engine.CreateMatchState(TarnFixtures.BuildSetup(
            seed: 1,
            championOne: "CH001",
            deckOne: ["UN014"],
            championTwo: "CH020",
            deckTwo: ["SP006"]));

        engine.PlaySingleRound(state);

        Assert.Equal(20, state.PlayerTwo.Champion.Health);
        Assert.DoesNotContain(state.ReplayLog, line => line.Contains("P1-UN014", StringComparison.Ordinal) && line.Contains("attacks", StringComparison.Ordinal));
    }

    [Fact]
    public void DefenderAbsorbsEnemyUnitAttackDamageToChampion()
    {
        var engine = new GameEngine();
        var state = engine.CreateMatchState(TarnFixtures.BuildSetup(
            seed: 2,
            championOne: "CH001",
            deckOne: ["SP006"],
            championTwo: "CH020",
            deckTwo: ["SP006"]));

        state.PlayerOne.Battlefield.Add(CreateUnit("P1", "UN014", zoneOrder: 1, enteredRound: 0));
        state.PlayerTwo.Battlefield.Add(CreateUnit("P2", "UN001", zoneOrder: 2, enteredRound: 0));

        engine.PlaySingleRound(state);

        Assert.Equal(20, state.PlayerTwo.Champion.Health);
        Assert.Empty(state.PlayerTwo.Battlefield);
        Assert.Contains(state.ReplayLog, line => line.Contains("assigned to Defender", StringComparison.Ordinal));
    }

    [Fact]
    public void MagnetRedirectsTargetedUnitSpellEffects()
    {
        var engine = new GameEngine();
        var state = engine.CreateMatchState(TarnFixtures.BuildSetup(
            seed: 3,
            championOne: "CH001",
            deckOne: ["SP001"],
            championTwo: "CH020",
            deckTwo: ["SP006"]));

        var olderNormal = CreateUnit("P2", "UN015", zoneOrder: 1, enteredRound: 0);
        var newerMagnet = CreateUnit("P2", "UN005", zoneOrder: 2, enteredRound: 0);
        state.PlayerTwo.Battlefield.Add(olderNormal);
        state.PlayerTwo.Battlefield.Add(newerMagnet);

        engine.PlaySingleRound(state);

        Assert.Equal(5, olderNormal.Health);
        Assert.Equal(0, newerMagnet.Health);
        Assert.Contains(state.ReplayLog, line => line.Contains(newerMagnet.InstanceId, StringComparison.Ordinal) && line.Contains("takes 2 damage", StringComparison.Ordinal));
    }

    [Fact]
    public void CounterCanCounterCounter()
    {
        var engine = new GameEngine();
        var state = engine.CreateMatchState(TarnFixtures.BuildSetup(
            seed: 4,
            championOne: "CH001",
            deckOne: ["SP001"],
            championTwo: "CH020",
            deckTwo: ["SP006"]));

        state.PlayerOne.CounterZone.Add(new CounterState
        {
            InstanceId = "P1-CT003-prep",
            OwnerId = "P1",
            Card = (CounterCardDefinition)TarnCardRegistry.Get("CT003"),
            ZoneOrder = 1,
        });
        state.PlayerTwo.CounterZone.Add(new CounterState
        {
            InstanceId = "P2-CT001-prep",
            OwnerId = "P2",
            Card = (CounterCardDefinition)TarnCardRegistry.Get("CT001"),
            ZoneOrder = 2,
        });
        state.PlayerTwo.Battlefield.Add(CreateUnit("P2", "UN014", zoneOrder: 3, enteredRound: 0));

        engine.PlaySingleRound(state);

        Assert.Empty(state.PlayerTwo.Battlefield);
        Assert.Contains(state.ReplayLog, line => line.Contains("CT003 counters", StringComparison.Ordinal));
        Assert.Contains(state.ReplayLog, line => line.Contains("P2-CT001-prep counter effect is countered", StringComparison.Ordinal));
    }

    [Fact]
    public void OnDeathHappensBeforeLeavePlayAndOnDestroyedAfter()
    {
        var engine = new GameEngine();
        var state = engine.CreateMatchState(TarnFixtures.BuildSetup(
            seed: 5,
            championOne: "CH020",
            deckOne: ["SP006"],
            championTwo: "CH001",
            deckTwo: ["SP002"]));

        var anchor = CreateUnit("P1", "UN015", zoneOrder: 1, enteredRound: 0);
        var graveTender = CreateUnit("P1", "UN009", zoneOrder: 2, enteredRound: 0);
        var ashDrifter = CreateUnit("P1", "UN010", zoneOrder: 3, enteredRound: 0);
        state.PlayerOne.Battlefield.Add(anchor);
        state.PlayerOne.Battlefield.Add(graveTender);
        state.PlayerOne.Battlefield.Add(ashDrifter);

        engine.PlaySingleRound(state);

        var deathTriggerIndex = IndexOf(state.ReplayLog, "UN009 On Death");
        var graveTenderLeavesIndex = IndexOf(state.ReplayLog, "P1-UN009-2 leaves the Battlefield");
        var ashLeavesIndex = IndexOf(state.ReplayLog, "P1-UN010-3 leaves the Battlefield");
        var destroyedTriggerIndex = IndexOf(state.ReplayLog, "UN010 On Destroyed");

        Assert.True(deathTriggerIndex >= 0 && graveTenderLeavesIndex >= 0 && deathTriggerIndex < graveTenderLeavesIndex);
        Assert.True(ashLeavesIndex >= 0 && destroyedTriggerIndex >= 0 && ashLeavesIndex < destroyedTriggerIndex);
    }

    [Fact]
    public void DoubleLethalEntersOvertimeInsteadOfDraw()
    {
        var engine = new GameEngine();
        var state = engine.CreateMatchState(TarnFixtures.BuildSetup(
            seed: 6,
            championOne: "CH019",
            deckOne: ["CT001"],
            championTwo: "CH019",
            deckTwo: ["CT001"]));

        state.PlayerOne.Champion.Health = 0;
        state.PlayerTwo.Champion.Health = 0;

        engine.PlaySingleRound(state);

        Assert.True(state.OvertimePending);
        Assert.Null(state.WinnerPlayerId);
        Assert.Contains(state.ReplayLog, line => line.Contains("Enter Overtime", StringComparison.Ordinal));
    }

    private static UnitState CreateUnit(string ownerId, string cardId, long zoneOrder, int enteredRound)
    {
        var card = (UnitCardDefinition)TarnCardRegistry.Get(cardId);
        return new UnitState
        {
            InstanceId = $"{ownerId}-{cardId}-{zoneOrder}",
            OwnerId = ownerId,
            Card = card,
            EnteredRound = enteredRound,
            ZoneOrder = zoneOrder,
            Attack = card.Attack,
            Health = card.Health,
            HasDefender = card.HasDefender,
            HasMagnet = card.HasMagnet,
        };
    }

    private static int IndexOf(IReadOnlyList<string> lines, string fragment)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Contains(fragment, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
