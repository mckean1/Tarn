using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public static class CardTextFormatter
{
    public static string BuildStats(CardDefinition definition) => definition.Type switch
    {
        CardType.Unit or CardType.Champion => $"ATK {definition.Attack} | HP {definition.Health} | SPD {definition.Speed}",
        _ => "No combat stats",
    };

    public static string? BuildCollectionStats(CardDefinition definition) => definition.Type switch
    {
        CardType.Unit or CardType.Champion => $"ATK {definition.Attack} · HP {definition.Health} · SPD {definition.Speed}",
        _ => null,
    };

    public static string BuildKeywordSummary(IReadOnlyList<string> keywords)
        => keywords.Count == 0 ? "None" : string.Join(", ", keywords);
}
