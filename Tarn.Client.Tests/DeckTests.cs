using Tarn.ClientApp.Play.Queries;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class DeckTests
{
    [Fact]
    public void DeckLegalitySummaryUsesLegalTag()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var validation = DeckValidator.ValidateSubmittedDeck(world, human, human.ActiveDeck!);

        Assert.Equal("[LEGAL]", DeckQueries.BuildLegalitySummary(validation));
    }

    [Fact]
    public void GroupedDeckPresentationIncludesChampionAndUnits()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var model = new DeckQueries().Build(world, human.Id, 0);

        Assert.Equal("Champion", model.Entries[0].Group);
        Assert.Contains(model.Entries, entry => entry.Group == "Units");
    }
}
