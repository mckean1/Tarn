using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.Client.Tests;

public sealed class CollectionTests
{
    [Fact]
    public void CollectionFilterMatchesChampion()
    {
        var row = new CollectionRowViewModel("CH001", "Champion", "Champion", "Rare", 1, 1, "Rules", "Stats");
        Assert.True(CollectionQueries.MatchesFilter(row, CollectionFilter.Champions));
        Assert.False(CollectionQueries.MatchesFilter(row, CollectionFilter.Spells));
    }

    [Fact]
    public void CollectionSortByOwnedCountPutsLargestFirst()
    {
        var rows = new[]
        {
            new CollectionRowViewModel("A", "Alpha", "Unit", "Common", 2, 0, "", ""),
            new CollectionRowViewModel("B", "Beta", "Unit", "Common", 5, 0, "", ""),
        };

        var sorted = CollectionQueries.Sort(rows, CollectionSort.OwnedCount).ToList();

        Assert.Equal("Beta", sorted[0].Name);
    }

    [Fact]
    public void CardRowTruncationUsesCleanSuffix()
    {
        var truncated = Layout.Truncate("Extremely Long Card Name", 10);
        Assert.Equal("Extremely.", truncated);
    }
}
