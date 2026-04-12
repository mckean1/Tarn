using System.Text;

namespace Tarn.Domain;

public sealed class SeededRng
{
    private ulong state;

    public SeededRng(int seed)
    {
        state = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        var result = state * 2685821657736338717UL;
        return (int)(result % (uint)maxExclusive);
    }
}

public sealed class UnitState
{
    public required string InstanceId { get; init; }
    public required string OwnerId { get; init; }
    public required UnitCardDefinition Card { get; init; }
    public required int EnteredRound { get; set; }
    public required long ZoneOrder { get; set; }
    public int Attack { get; set; }
    public int Health { get; set; }
    public bool HasDefender { get; set; }
    public bool HasMagnet { get; set; }
    public bool IsInDeathProcess { get; set; }
    public bool IsDead => Health <= 0;
}

public sealed class ChampionState
{
    public required ChampionCardDefinition Card { get; init; }
    public required string OwnerId { get; init; }
    public int Health { get; set; }
    public int Attack { get; set; }
    public bool TookDamageThisRound { get; set; }
    public bool EnemyChampionTookDamageThisRound { get; set; }
}

public sealed class CounterState
{
    public required string InstanceId { get; init; }
    public required string OwnerId { get; init; }
    public required CounterCardDefinition Card { get; init; }
    public required long ZoneOrder { get; set; }
}

public sealed class PlayerState
{
    public required string Id { get; init; }
    public required DeckDefinition Deck { get; init; }
    public required ChampionState Champion { get; init; }
    public Queue<CardDefinition> DeckCards { get; set; } = new();
    public List<UnitState> Battlefield { get; } = [];
    public List<CounterState> CounterZone { get; } = [];
    public List<CardDefinition> Discard { get; } = [];
    public int FatigueCount { get; set; }
}

public sealed class MatchSetup
{
    public required int Seed { get; init; }
    public required DeckDefinition PlayerOneDeck { get; init; }
    public required DeckDefinition PlayerTwoDeck { get; init; }
    public string PlayerOneId { get; init; } = "P1";
    public string PlayerTwoId { get; init; } = "P2";
    public bool ShuffleDecks { get; init; } = true;
    public InitiativeContext? Initiative { get; init; }
}

public sealed class SimulatedMatchResult
{
    public required int Seed { get; init; }
    public required string WinnerPlayerId { get; init; }
    public required int RoundCount { get; init; }
    public required int PlayerOneChampionHealth { get; init; }
    public required int PlayerTwoChampionHealth { get; init; }
    public required IReadOnlyList<string> ReplayLog { get; init; }
    public required string ReplayText { get; init; }
}

public sealed class MatchState
{
    public required MatchSetup Setup { get; init; }
    public required SeededRng Rng { get; init; }
    public required PlayerState PlayerOne { get; init; }
    public required PlayerState PlayerTwo { get; init; }
    public required int Seed { get; init; }
    public int RoundNumber { get; set; }
    public bool OvertimePending { get; set; }
    public string? WinnerPlayerId { get; set; }
    public int InitiativePlayerIndex { get; set; }
    public long NextZoneOrder { get; set; } = 1;
    public long NextLogSequence { get; set; } = 1;
    public List<string> ReplayLog { get; } = [];

    public IReadOnlyList<PlayerState> Players => [PlayerOne, PlayerTwo];

    public PlayerState GetPlayer(int index) => index == 0 ? PlayerOne : PlayerTwo;

    public PlayerState GetPlayer(string id) =>
        Players.Single(player => string.Equals(player.Id, id, StringComparison.Ordinal));

    public PlayerState GetOpponent(string id) =>
        Players.Single(player => !string.Equals(player.Id, id, StringComparison.Ordinal));

    public int GetPlayerIndex(string id) => string.Equals(PlayerOne.Id, id, StringComparison.Ordinal) ? 0 : 1;

    public void Log(string message)
    {
        ReplayLog.Add($"{NextLogSequence++,4} | {message}");
    }

    public string BuildReplayText()
    {
        var builder = new StringBuilder();
        foreach (var entry in ReplayLog)
        {
            builder.AppendLine(entry);
        }

        return builder.ToString();
    }
}
