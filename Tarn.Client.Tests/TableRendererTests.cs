using Tarn.ClientApp.Play.Rendering;

namespace Tarn.Client.Tests;

public sealed class TableRendererTests
{
    [Fact]
    public void RendersHeadersAndTruncatesValues()
    {
        var output = TableRenderer.Render(
        [
            new TableColumn { Header = "Name", Width = 8 },
            new TableColumn { Header = "Type", Width = 6 },
        ],
        [
            new[] { "Very Long Name", "Counter" },
        ]);

        Assert.Contains("Name    |Type  ", output);
        Assert.Contains("Very Lo.|Count.", output);
    }
}
