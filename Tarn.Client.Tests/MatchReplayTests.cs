using Tarn.ClientApp.Play.Queries;

namespace Tarn.Client.Tests;

public sealed class MatchReplayTests
{
    [Theory]
    [InlineData("   1 | P1 Champion: Veyn", "Home Champion: Veyn")]
    [InlineData("   2 | P2 plays UN014 Hearthblade Initiate.", "Away plays UN014 Hearthblade Initiate.")]
    [InlineData("  20 | Enter Overtime", "[OVERTIME] The match enters overtime.")]
    [InlineData("  21 | P1 is out of cards and takes Fatigue 1.", "[FATIGUE] Home is out of cards and takes Fatigue 1.")]
    public void PhrasesReplayEvents(string raw, string expected)
    {
        Assert.Equal(expected, MatchReplayQueries.PhraseEvent(raw));
    }
}
