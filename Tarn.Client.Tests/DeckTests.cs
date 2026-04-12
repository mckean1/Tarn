using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.Deck;
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
        Assert.Contains(model.Groups, group => group.Name == "Spells");
    }

    [Fact]
    public void GroupedEntriesAggregateDuplicateCopies()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var card = world.GetLatestDefinition(world.StandardSetIds.SelectMany(id => world.CardSets[id].CardIds).First());

        var entries = DeckQueries.GroupedEntries("Units", [card, card, card]);

        Assert.Single(entries);
        Assert.Equal(3, entries[0].CopiesInDeck);
    }

    [Fact]
    public void DeckRendererUsesSummaryBrowseAndDetailPanes()
    {
        var state = BuildState();
        var champion = new DeckEntryViewModel("Champion", "CH-1", "Veyn, Ember Edge", "Champion", "Legendary", "Champion. Swift", "ATK 5 · HP 22 · SPD 3", "Swift", 1, 1);
        var unit = new DeckEntryViewModel("Units", "UN-7", "Ashen Scout", "Unit", "Common", "Unit. No keyword.", "ATK 1 · HP 4 · SPD 0", "None", 3, 3);
        state.Deck.SelectedIndex = 1;
        state.Deck.ViewModel = new DeckViewModel(
            "[LEGAL]",
            "31/31 cards",
            "Power 30/100",
            "Units 30 · Spells 0 · Counters 0",
            "Veyn, Ember Edge",
            [
                new DeckGroupViewModel("Champion", [champion], "No champion selected."),
                new DeckGroupViewModel("Units", [unit], "No units in deck."),
                new DeckGroupViewModel("Spells", [], "No spells in deck."),
                new DeckGroupViewModel("Counters", [], "No counters in deck."),
            ],
            [champion, unit],
            new DeckDetailViewModel(unit.Name, unit.Type, unit.Rarity, unit.StatsText, unit.RulesText, unit.KeywordsText, unit.CopiesInDeck, unit.OwnedCount),
            1);

        var output = DeckRenderer.Render(state, new Rect(0, 0, 96, 22));
        var plainOutput = AnsiUtility.StripAnsi(output);

        Assert.Contains("┌ Deck Summary", plainOutput);
        Assert.Contains("Deck [LEGAL]", plainOutput);
        Assert.Contains("31/31 cards · Power 30/100", plainOutput);
        Assert.Contains("Champion: Veyn, Ember Edge", plainOutput);
        Assert.Contains("┌ Deck Contents", plainOutput);
        Assert.Contains("Champion (1)", plainOutput);
        Assert.Contains("Units (3)", plainOutput);
        Assert.Contains("> Ashen Scout", plainOutput);
        Assert.Contains("No spells in deck.", plainOutput);
        Assert.Contains("┌ Selected Card", plainOutput);
        Assert.Contains("In deck: 3", plainOutput);
        Assert.Contains("Keywords: None", plainOutput);
        Assert.DoesNotContain("Enter auto-builds the best legal deck.", plainOutput);

        if (TerminalStyle.SupportsAnsi)
        {
            Assert.Contains(TerminalStyle.BrightWhite, output);
        }
    }

    private static AppState BuildState()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        return new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
    }
}
