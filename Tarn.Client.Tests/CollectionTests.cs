using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.Collection;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class CollectionTests
{
    [Fact]
    public void CollectionFilterMatchesChampion()
    {
        var row = new CollectionRowViewModel("CH001", "Champion", CardType.Champion, CardRarity.Rare, 1, 1, "ATK 2 · HP 5 · SPD 1", "Swift", "Rules");
        Assert.True(CollectionQueries.MatchesFilter(row, CollectionFilter.Champions));
        Assert.False(CollectionQueries.MatchesFilter(row, CollectionFilter.Spells));
    }

    [Fact]
    public void CollectionSortByOwnedCountPutsLargestFirst()
    {
        var rows = new[]
        {
            new CollectionRowViewModel("A", "Alpha", CardType.Unit, CardRarity.Common, 2, 0, "ATK 1 · HP 2 · SPD 0", "None", "Rules"),
            new CollectionRowViewModel("B", "Beta", CardType.Unit, CardRarity.Common, 5, 0, "ATK 1 · HP 2 · SPD 0", "None", "Rules"),
        };

        var sorted = CollectionQueries.Sort(rows, CollectionSort.OwnedCount).ToList();

        Assert.Equal("Beta", sorted[0].Name);
    }

    [Fact]
    public void CollectionRendererShowsFilterSortAndAlignedRows()
    {
        var rows = new[]
        {
            new CollectionRowViewModel("A", "Ashen Scout", CardType.Unit, CardRarity.Common, 3, 1, "ATK 1 · HP 4 · SPD 0", "None", "Unit rules."),
            new CollectionRowViewModel("B", "Veyn, Ember Edge With A Very Long Title", CardType.Champion, CardRarity.Legendary, 12, 3, "ATK 4 · HP 8 · SPD 2", "Swift", "Champion rules."),
        };
        var viewModel = new CollectionViewModel(
            "Owned Only",
            "Owned Count",
            1,
            rows,
            new CollectionDetailViewModel("Veyn, Ember Edge With A Very Long Title", "Champion", "Legendary", "ATK 4 · HP 8 · SPD 2", 12, 3, "Swift", "Champion rules."));
        var state = BuildState(viewModel, selectedIndex: 1);

        var output = CollectionRenderer.Render(state, new Rect(0, 0, 100, 16));
        var plainLines = AnsiUtility.StripAnsi(output).Split(Environment.NewLine);
        var styledLines = output.Split(Environment.NewLine);
        var unitLine = plainLines.Single(line => line.Contains("Ashen Scout x3", StringComparison.Ordinal));
        var championLine = plainLines.Single(line => line.Contains("Veyn, Ember Edge", StringComparison.Ordinal));

        Assert.Contains("Filter: Owned Only", output);
        Assert.Contains("Sort: Owned Count", output);
        Assert.Equal(unitLine.IndexOf("Unit", StringComparison.Ordinal), championLine.IndexOf("Champion", StringComparison.Ordinal));
        Assert.Matches(@"Common\s+3\s*│", unitLine);
        Assert.Matches(@"Legendary\s+12\s*│", championLine);
        Assert.Contains("│> ", championLine);
        Assert.Contains("│  ", unitLine);

        if (TerminalStyle.SupportsAnsi)
        {
            Assert.Contains(TerminalStyle.BrightWhite, styledLines.Single(line => line.Contains("Veyn, Ember Edge", StringComparison.Ordinal)));
            Assert.DoesNotContain(TerminalStyle.BrightWhite, styledLines.Single(line => line.Contains("Ashen Scout x3", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void CollectionRendererShowsUnitDetailPane()
    {
        var detail = new CollectionDetailViewModel("Ashen Scout", "Unit", "Common", "ATK 1 · HP 4 · SPD 0", 3, 2, "Defender", "Hold the line.");
        var state = BuildState(new CollectionViewModel("All", "Name", 0,
        [
            new CollectionRowViewModel("A", "Ashen Scout", CardType.Unit, CardRarity.Common, 3, 2, "ATK 1 · HP 4 · SPD 0", "Defender", "Hold the line."),
        ],
        detail));

        var output = AnsiUtility.StripAnsi(CollectionRenderer.Render(state, new Rect(0, 0, 100, 14)));

        Assert.Contains("Selected Card", output);
        Assert.Contains("Ashen Scout", output);
        Assert.Contains("Type: Unit", output);
        Assert.Contains("Rarity: Common", output);
        Assert.Contains("ATK 1 · HP 4 · SPD 0", output);
        Assert.Contains("Owned: 3", output);
        Assert.Contains("In active deck: 2", output);
        Assert.Contains("Keywords: Defender", output);
    }

    [Fact]
    public void CollectionRendererShowsChampionDetailPane()
    {
        var detail = new CollectionDetailViewModel("Veyn, Ember Edge", "Champion", "Rare", "ATK 3 · HP 7 · SPD 1", 1, 1, "Swift", "Champion. Swift.");
        var state = BuildState(new CollectionViewModel("Champions", "Rarity", 0,
        [
            new CollectionRowViewModel("C", "Veyn, Ember Edge", CardType.Champion, CardRarity.Rare, 1, 1, "ATK 3 · HP 7 · SPD 1", "Swift", "Champion. Swift."),
        ],
        detail));

        var output = AnsiUtility.StripAnsi(CollectionRenderer.Render(state, new Rect(0, 0, 100, 14)));

        Assert.Contains("Type: Champion", output);
        Assert.Contains("Rarity: Rare", output);
        Assert.Contains("ATK 3 · HP 7 · SPD 1", output);
        Assert.Contains("Keywords: Swift", output);
    }

    [Fact]
    public void CollectionRendererShowsSpellDetailWithoutCombatStats()
    {
        var detail = new CollectionDetailViewModel("Lantern Flare", "Spell", "Epic", null, 2, 0, "Burst", "Deal 3 damage to an enemy unit.");
        var state = BuildState(new CollectionViewModel("Spells", "Name", 0,
        [
            new CollectionRowViewModel("S", "Lantern Flare", CardType.Spell, CardRarity.Epic, 2, 0, null, "Burst", "Deal 3 damage to an enemy unit."),
        ],
        detail));

        var output = AnsiUtility.StripAnsi(CollectionRenderer.Render(state, new Rect(0, 0, 100, 14)));

        Assert.Contains("Type: Spell", output);
        Assert.Contains("Rarity: Epic", output);
        Assert.Contains("Owned: 2", output);
        Assert.Contains("Keywords: Burst", output);
        Assert.DoesNotContain("ATK ", output);
    }

    [Fact]
    public void CollectionRendererShowsIntentionalEmptyState()
    {
        var state = BuildState(new CollectionViewModel("Owned Only", "Name", 0, [], null));

        var output = AnsiUtility.StripAnsi(CollectionRenderer.Render(state, new Rect(0, 0, 100, 14)));

        Assert.Contains("[No Cards]", output);
        Assert.Contains("No cards match this filter.", output);
        Assert.Contains("No card selected.", output);
    }

    private static AppState BuildState(CollectionViewModel viewModel, int selectedIndex = 0)
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState
        {
            ActiveScreen = ScreenId.Collection,
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
        state.Collection.ViewModel = viewModel;
        state.Collection.SelectedIndex = selectedIndex;
        return state;
    }
}
