using Tarn.ClientApp.Play.Queries;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class CardDisplayGrouperTests
{
    private static readonly string[] PlaceholderPrefixes = ["Unit ", "Spell ", "Counter ", "Player "];

    [Fact]
    public void GroupsDuplicateCardsByStableDefinitionId()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var championId = world.CardSets[world.StandardSetIds[0]].CardIds.Select(world.GetLatestDefinition).First(card => card.Type == CardType.Champion).Id;
        var unitId = world.CardSets[world.StandardSetIds[0]].CardIds.Select(world.GetLatestDefinition).First(card => card.Type == CardType.Unit).Id;

        var grouped = CardDisplayGrouper.GroupCardIds(world, [unitId, championId, unitId, unitId]);

        Assert.Equal(2, grouped.Count);
        Assert.Equal(championId, grouped[0].CardId);
        Assert.Equal(1, grouped[0].Count);
        Assert.Equal(unitId, grouped[1].CardId);
        Assert.Equal(3, grouped[1].Count);
        Assert.Equal("Ashen Scout x3", grouped[1].DisplayName);
    }

    [Fact]
    public void SortOrderIsTypeThenRarityThenDisplayName()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var setDefinitions = world.CardSets[world.StandardSetIds[0]].CardIds.Select(world.GetLatestDefinition).ToList();
        var rareChampion = setDefinitions.First(card => card.Type == CardType.Champion && card.Rarity == CardRarity.Rare);
        var commonUnit = setDefinitions.First(card => card.Type == CardType.Unit && card.Rarity == CardRarity.Common);
        var rareSpell = setDefinitions.First(card => card.Type == CardType.Spell && card.Rarity == CardRarity.Rare);
        var commonCounter = setDefinitions.First(card => card.Type == CardType.Counter && card.Rarity == CardRarity.Common);

        var grouped = CardDisplayGrouper.GroupCardIds(world, [commonCounter.Id, rareSpell.Id, commonUnit.Id, rareChampion.Id]);

        Assert.Equal([CardType.Champion, CardType.Unit, CardType.Spell, CardType.Counter], grouped.Select(entry => entry.Type));
    }

    [Fact]
    public void StarterPoolDisplayNamesUseAuthoredNames()
    {
        Assert.Equal("Ashen Scout", TarnCardRegistry.Units["UN001"].Name);
        Assert.Equal("Blackbriar Sentinel", TarnCardRegistry.Units["UN020"].Name);
        Assert.Equal("Iron Rite", TarnCardRegistry.Spells["SP001"].Name);
        Assert.Equal("Lantern Flare", TarnCardRegistry.Spells["SP006"].Name);
        Assert.Equal("Bastion Ward", TarnCardRegistry.Counters["CT001"].Name);
        Assert.Equal("Last Denial", TarnCardRegistry.Counters["CT004"].Name);

        var allNames = TarnCardRegistry.Units.Values.Select(card => card.Name)
            .Concat(TarnCardRegistry.Spells.Values.Select(card => card.Name))
            .Concat(TarnCardRegistry.Counters.Values.Select(card => card.Name));

        Assert.DoesNotContain(allNames, name => PlaceholderPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void AiPlayersUseDeterministicNonPlaceholderNamesWithinLeague()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var bronzePlayers = world.Players.Values
            .Where(player => player.League == LeagueTier.Bronze)
            .Select(player => player.Name)
            .ToList();

        Assert.Contains("You", bronzePlayers);
        Assert.Equal(bronzePlayers.Count, bronzePlayers.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(bronzePlayers, name => name.StartsWith("Player ", StringComparison.Ordinal));
    }
}
