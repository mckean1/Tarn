using Tarn.Domain;

namespace Tarn.ClientApp.Play.Queries;

public static class PlayerNameFormatter
{
    public static string Format(World world, string humanPlayerId, string playerId)
        => string.Equals(playerId, humanPlayerId, StringComparison.Ordinal) ? "You" : world.Players[playerId].Name;
}
