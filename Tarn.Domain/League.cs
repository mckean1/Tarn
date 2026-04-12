namespace Tarn.Domain;

public enum LeagueTier
{
    Bronze,
    Silver,
    Gold,
    World,
}

public enum PromotionStatus
{
    None,
    Promoted,
    Relegated,
}

public sealed record SeasonalRuleChange(string RuleId, string Description);

public sealed record SeasonDefinition(
    int Year,
    SeasonalRuleChange? RuleChange,
    IReadOnlyList<string> AddedCardIds,
    IReadOnlyList<string> BalancedCardIds);

public sealed class StandingsEntry
{
    public required string DeckId { get; init; }
    public required LeagueTier League { get; init; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int MatchPoints { get; set; }
    public int GameDifferential { get; set; }
    public int LeagueRank { get; set; }
    public bool QualifiedForPlayoffs { get; set; }
    public PromotionStatus PromotionStatus { get; set; }
}

public sealed record GameResult(int WinnerGames, int LoserGames, string WinnerPlayerId, string LoserPlayerId);

public sealed class MatchResult
{
    public required string WinnerPlayerId { get; init; }
    public required string LoserPlayerId { get; init; }
    public required int WinnerGameWins { get; init; }
    public required int LoserGameWins { get; init; }
    public required IReadOnlyList<GameResult> Games { get; init; }

    public int WinnerMatchPoints => WinnerGameWins == 2 && LoserGameWins == 0 ? 3 : 2;
    public int LoserMatchPoints => WinnerGameWins == 2 && LoserGameWins == 1 ? 1 : 0;
    public int WinnerGameDifferential => WinnerGameWins - LoserGameWins;
    public int LoserGameDifferential => LoserGameWins - WinnerGameWins;
}

public static class StandingsCalculator
{
    public static void ApplyMatchResult(
        MatchResult match,
        IDictionary<string, StandingsEntry> standings)
    {
        var winner = standings[match.WinnerPlayerId];
        var loser = standings[match.LoserPlayerId];

        winner.Wins += 1;
        loser.Losses += 1;
        winner.MatchPoints += match.WinnerMatchPoints;
        loser.MatchPoints += match.LoserMatchPoints;
        winner.GameDifferential += match.WinnerGameDifferential;
        loser.GameDifferential += match.LoserGameDifferential;
    }

    public static IReadOnlyList<StandingsEntry> Rank(IReadOnlyCollection<StandingsEntry> standings)
    {
        var ranked = standings
            .OrderByDescending(entry => entry.MatchPoints)
            .ThenByDescending(entry => entry.GameDifferential)
            .ThenBy(entry => entry.DeckId, StringComparer.Ordinal)
            .ToList();

        for (var index = 0; index < ranked.Count; index++)
        {
            ranked[index].LeagueRank = index + 1;
            ranked[index].QualifiedForPlayoffs = index < 8;
        }

        return ranked;
    }
}
