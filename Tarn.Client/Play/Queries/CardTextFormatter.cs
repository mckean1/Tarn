using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public static class CardTextFormatter
{
    public static string BuildStats(CardDefinition definition) => definition.Type switch
    {
        CardType.Unit or CardType.Champion => $"ATK {definition.Attack} | HP {definition.Health} | SPD {definition.Speed}",
        _ => "No combat stats",
    };
}
