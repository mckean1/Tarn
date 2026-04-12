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

    [Fact]
    public void BuildRowSupportsRightAlignedColumnsWithCustomSeparators()
    {
        var row = TableRenderer.BuildRow(
        [
            "Extremely Long Card Name",
            "Champion",
            "Legendary",
            "12",
        ],
        [
            new TableColumn { Header = "Name", Width = 20 },
            new TableColumn { Header = "Type", Width = 10 },
            new TableColumn { Header = "Rarity", Width = 10 },
            new TableColumn { Header = "Owned", Width = 5, Alignment = TableCellAlignment.Right },
        ],
        columnSeparator: "  ");

        Assert.Equal("Extremely Long Card.  Champion    Legendary      12", row);
        Assert.Equal(51, AnsiUtility.GetVisibleLength(row));
    }

    [Fact]
    public void InteractiveRowAppliesSharedSelectionEmphasis()
    {
        var selected = ScreenText.InteractiveRow(true, "Advance Week");
        var normal = ScreenText.InteractiveRow(false, "Advance Week");

        Assert.Contains("> Advance Week", selected);
        Assert.Equal("  Advance Week", normal);
        Assert.DoesNotContain(TerminalStyle.BrightWhite, normal);

        if (TerminalStyle.SupportsAnsi)
        {
            Assert.Contains(TerminalStyle.BrightWhite, selected);
            Assert.EndsWith(TerminalStyle.Reset, selected);
        }
    }

    [Fact]
    public void TableRendererStylesSelectedRowsWithoutBreakingAlignment()
    {
        var output = TableRenderer.Render(
        [
            new TableColumn { Header = "Name", Width = 8 },
            new TableColumn { Header = "Type", Width = 6 },
        ],
        [
            new[] { "Alpha", "Unit" },
            new[] { "Beta", "Spell" },
        ],
        selectedRowIndex: 1);
        var lines = output.Split(Environment.NewLine);

        Assert.Contains("Beta    |Spell ", output);
        Assert.All(lines, line => Assert.Equal(15, AnsiUtility.GetVisibleLength(line)));

        if (TerminalStyle.SupportsAnsi)
        {
            Assert.Contains(TerminalStyle.BrightWhite + "Beta    |Spell " + TerminalStyle.Reset, output);
        }
    }
}
