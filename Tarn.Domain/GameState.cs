namespace Tarn.Domain;

public enum RoundStep
{
    None,
    Play,
    Quick,
    Resolution,
    Attack,
    End,
    Destruction,
    LastWish,
    WinCheck,
}

public sealed class GameLogEntry
{
    public required long Sequence { get; init; }
    public required int Round { get; init; }
    public required RoundStep Step { get; init; }
    public required string Message { get; init; }
}

public sealed class ReplayEvent
{
    public required long Sequence { get; init; }
    public required TriggerEventType EventType { get; init; }
    public required string SourcePlayerId { get; init; }
    public string? SourceCardId { get; init; }
    public string? SourceInstanceId { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}

public sealed class CombatCardState
{
    public required string InstanceId { get; init; }
    public required string OwnerId { get; init; }
    public required CardDefinition Definition { get; init; }
    public required int EnteredRound { get; set; }
    public required long PlayOrder { get; set; }
    public int CurrentHealth { get; set; }
    public int TemporaryAttackModifier { get; set; }
    public bool PreventAttackThisRound { get; set; }
    public bool MarkedForDestruction { get; set; }
    public bool DestroyedThisRound { get; set; }

    public CardType Type => Definition.Type;
    public int PrintedAttack => Definition.Attack;
    public int PrintedHealth => Definition.Health;
    public int CurrentAttack => Math.Max(0, PrintedAttack + TemporaryAttackModifier);
    public Keyword Keywords => Definition.Keywords;
}

public sealed class CounterState
{
    public required string InstanceId { get; init; }
    public required string OwnerId { get; init; }
    public required CounterDefinition Definition { get; init; }
    public required long PlayOrder { get; set; }
    public required long SetSequence { get; set; }
}

public sealed class PlayedCardState
{
    public required string PlayerId { get; init; }
    public required CardDefinition Card { get; init; }
}

public sealed class PlayerState
{
    public required string Id { get; init; }
    public required DeckDefinition Deck { get; init; }
    public required CombatCardState Champion { get; set; }
    public Queue<CardDefinition> Library { get; set; } = new();
    public List<CombatCardState> Board { get; } = [];
    public List<CounterState> Counters { get; } = [];
    public int FatigueCount { get; set; }
}

public sealed class GameState
{
    public required PlayerState PlayerOne { get; init; }
    public required PlayerState PlayerTwo { get; init; }
    public required string HigherSeedPlayerId { get; init; }
    public required string PriorityPlayerId { get; set; }
    public required int Seed { get; init; }
    public int RoundNumber { get; set; }
    public int GameNumber { get; set; } = 1;
    public int OvertimeCount { get; set; }
    public RoundStep CurrentStep { get; set; }
    public string? WinnerPlayerId { get; set; }
    public long Sequence { get; set; }
    public List<GameLogEntry> Logs { get; } = [];
    public List<ReplayEvent> Replay { get; } = [];
    public List<CombatCardState> DestroyedThisRound { get; } = [];

    public IEnumerable<PlayerState> Players
    {
        get
        {
            yield return PlayerOne;
            yield return PlayerTwo;
        }
    }

    public PlayerState GetPlayer(string id) =>
        Players.Single(player => string.Equals(player.Id, id, StringComparison.Ordinal));

    public PlayerState GetOpponent(string id) =>
        Players.Single(player => !string.Equals(player.Id, id, StringComparison.Ordinal));

    public long NextSequence() => ++Sequence;
}
