using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Collector;

public static class CollectorRenderer
{
    private const string ColumnSeparator = "  ";

    public static string Render(AppState state, Rect body)
    {
        var model = state.Collector.ViewModel;
        if (model is null)
        {
            return "Collector unavailable.";
        }

        return body.Width >= 88 && body.Height >= 12
            ? RenderTwoColumns(state, model, body)
            : RenderStacked(state, model, body);
    }

    private static string RenderTwoColumns(AppState state, CollectorViewModel model, Rect body)
    {
        const int gap = 2;
        var leftWidth = Math.Max(42, ((body.Width - gap) * 3) / 5);
        var (left, right) = body.SplitColumns(leftWidth, gap);
        var browseBox = BoxDrawing.RenderBox("Collector", BuildBrowsePaneLines(state, model, left.GetInnerRect()), left.Width, left.Height);
        var detailBox = BoxDrawing.RenderBox(BuildDetailTitle(model.Tab), BuildDetailPaneLines(model.Tab, model.Detail, right.GetInnerRect()), right.Width, right.Height);
        return string.Join(Environment.NewLine, BoxDrawing.MergeColumns(browseBox, detailBox, gap));
    }

    private static string RenderStacked(AppState state, CollectorViewModel model, Rect body)
    {
        var topHeight = Math.Max(7, body.Height / 2);
        var (top, bottom) = body.SplitRows(topHeight, 1);
        var lines = BoxDrawing.RenderBox("Collector", BuildBrowsePaneLines(state, model, top.GetInnerRect()), top.Width, top.Height)
            .Concat(BoxDrawing.RenderBox(BuildDetailTitle(model.Tab), BuildDetailPaneLines(model.Tab, model.Detail, bottom.GetInnerRect()), bottom.Width, bottom.Height));
        return string.Join(Environment.NewLine, lines.Take(body.Height));
    }

    private static IReadOnlyList<string> BuildBrowsePaneLines(AppState state, CollectorViewModel model, Rect rect)
    {
        var lines = new List<string>
        {
            Layout.Truncate(BuildTabStrip(model.Tab), rect.Width),
            string.Empty,
        };

        if (model.Rows.Count == 0)
        {
            lines.AddRange(BuildEmptyState(model.Tab, rect.Width));
            return lines.Take(Math.Max(1, rect.Height)).ToList();
        }

        var columns = BuildColumns(model.Tab, Math.Max(20, rect.Width - 2));
        lines.Add(ScreenText.Secondary(Layout.Truncate($"  {TableRenderer.BuildHeader(columns, ColumnSeparator)}", rect.Width)));
        lines.Add(ScreenText.Secondary(Layout.Truncate($"  {TableRenderer.BuildDivider(columns, ColumnSeparator, '─')}", rect.Width)));

        var visibleRows = Math.Max(1, rect.Height - lines.Count);
        var startIndex = Math.Clamp(state.Collector.SelectedIndex - (visibleRows / 2), 0, Math.Max(0, model.Rows.Count - visibleRows));
        var displayedRows = Math.Min(model.Rows.Count - startIndex, visibleRows);
        for (var index = 0; index < displayedRows; index++)
        {
            var rowIndex = startIndex + index;
            var row = model.Rows[rowIndex];
            var content = TableRenderer.BuildRow(BuildRowValues(model.Tab, row), columns, ColumnSeparator);
            lines.Add(Layout.Truncate(ScreenText.InteractiveRow(rowIndex == state.Collector.SelectedIndex, content), rect.Width));
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildDetailPaneLines(CollectorTab tab, CollectorRowViewModel? detail, Rect rect)
    {
        if (detail is null)
        {
            return BuildEmptyState(tab, rect.Width);
        }

        if (tab == CollectorTab.Packs)
        {
            var packLines = new List<string>
            {
                Layout.Truncate(detail.Name, rect.Width),
                string.Empty,
                Layout.Truncate("Type: Pack", rect.Width),
                Layout.Truncate($"Price: {detail.Price}", rect.Width),
                Layout.Truncate($"Status: {detail.Status}", rect.Width),
                string.Empty,
                ScreenText.Secondary("Contents"),
            };
            packLines.AddRange(ScreenText.WrapLines(detail.SummaryText, rect.Width));
            packLines.Add(string.Empty);
            packLines.Add(ScreenText.Secondary("Action"));
            packLines.Add(Layout.Truncate(detail.ActionLabel, rect.Width));
            return packLines.Take(Math.Max(1, rect.Height)).ToList();
        }

        var lines = new List<string>
        {
            Layout.Truncate(detail.Name, rect.Width),
            string.Empty,
            Layout.Truncate($"Type: {detail.Type}", rect.Width),
            Layout.Truncate($"Rarity: {detail.Rarity}", rect.Width),
        };

        if (!string.IsNullOrWhiteSpace(detail.StatsText))
        {
            lines.Add(Layout.Truncate(detail.StatsText, rect.Width));
        }

        lines.Add(Layout.Truncate($"Keywords: {detail.KeywordsText}", rect.Width));
        lines.Add(Layout.Truncate(detail.PriceLabel, rect.Width));
        lines.Add(Layout.Truncate($"Status: {detail.Status}", rect.Width));
        if (!string.IsNullOrWhiteSpace(detail.OwnedLabel))
        {
            lines.Add(Layout.Truncate(detail.OwnedLabel, rect.Width));
        }

        lines.Add(string.Empty);
        lines.Add(ScreenText.Secondary("Action"));
        lines.Add(Layout.Truncate(detail.ActionLabel, rect.Width));
        lines.Add(string.Empty);
        lines.Add(ScreenText.Secondary("Rules"));
        lines.AddRange(ScreenText.WrapLines(detail.RulesText, rect.Width));
        return lines.Take(Math.Max(1, rect.Height)).ToList();
    }

    private static IReadOnlyList<string> BuildEmptyState(CollectorTab tab, int width)
    {
        return tab switch
        {
            CollectorTab.Singles => ["[No Singles]", Layout.Truncate("The Collector has no singles right now.", width)],
            CollectorTab.Packs => ["[No Packs]", Layout.Truncate("No packs are on the shelf today.", width)],
            _ => ["[Nothing to Sell]", Layout.Truncate("You have no cards available for collector buyback.", width)],
        };
    }

    private static string BuildTabStrip(CollectorTab activeTab)
    {
        return string.Join("  ", Enum.GetValues<CollectorTab>().Select(tab => tab == activeTab ? ScreenText.StatusChip(tab.ToString()) : tab.ToString()));
    }

    private static string BuildDetailTitle(CollectorTab tab) => tab switch
    {
        CollectorTab.Packs => "Selected Pack",
        CollectorTab.Sell => "Sell Detail",
        _ => "Selected Item",
    };

    private static IReadOnlyList<string> BuildRowValues(CollectorTab tab, CollectorRowViewModel row) => tab switch
    {
        CollectorTab.Packs => [row.Name, row.Price.ToString(), row.SummaryText, row.Status],
        CollectorTab.Sell => [row.DisplayName, row.Type, ExtractOwnedValue(row.OwnedLabel), row.Price.ToString(), row.Status],
        _ => [row.Name, row.Type, row.Rarity, row.Price.ToString(), row.Status],
    };

    private static IReadOnlyList<TableColumn> BuildColumns(CollectorTab tab, int availableWidth)
    {
        var widths = tab switch
        {
            CollectorTab.Packs => new[] { 20, 6, 22, 15 },
            CollectorTab.Sell => new[] { 18, 10, 5, 9, 12 },
            _ => new[] { 18, 10, 10, 7, 15 },
        };
        var minimums = tab switch
        {
            CollectorTab.Packs => new[] { 12, 5, 12, 10 },
            CollectorTab.Sell => new[] { 12, 8, 5, 7, 10 },
            _ => new[] { 12, 8, 8, 6, 10 },
        };

        var targetWidth = widths.Sum() + (ColumnSeparator.Length * (widths.Length - 1));
        if (availableWidth >= targetWidth)
        {
            widths[0] += availableWidth - targetWidth;
        }
        else
        {
            var overflow = targetWidth - availableWidth;
            foreach (var index in Enumerable.Range(0, widths.Length))
            {
                if (overflow <= 0)
                {
                    break;
                }

                var reducible = widths[index] - minimums[index];
                if (reducible <= 0)
                {
                    continue;
                }

                var reduction = Math.Min(reducible, overflow);
                widths[index] -= reduction;
                overflow -= reduction;
            }
        }

        return tab switch
        {
            CollectorTab.Packs =>
            [
                new TableColumn { Header = "Pack", Width = widths[0] },
                new TableColumn { Header = "Price", Width = widths[1], Alignment = TableCellAlignment.Right },
                new TableColumn { Header = "Contents", Width = widths[2] },
                new TableColumn { Header = "Status", Width = widths[3] },
            ],
            CollectorTab.Sell =>
            [
                new TableColumn { Header = "Name", Width = widths[0] },
                new TableColumn { Header = "Type", Width = widths[1] },
                new TableColumn { Header = "Owned", Width = widths[2], Alignment = TableCellAlignment.Right },
                new TableColumn { Header = "Sell", Width = widths[3], Alignment = TableCellAlignment.Right },
                new TableColumn { Header = "Status", Width = widths[4] },
            ],
            _ =>
            [
                new TableColumn { Header = "Name", Width = widths[0] },
                new TableColumn { Header = "Type", Width = widths[1] },
                new TableColumn { Header = "Rarity", Width = widths[2] },
                new TableColumn { Header = "Price", Width = widths[3], Alignment = TableCellAlignment.Right },
                new TableColumn { Header = "Status", Width = widths[4] },
            ],
        };
    }

    private static string ExtractOwnedValue(string? ownedLabel)
    {
        if (string.IsNullOrWhiteSpace(ownedLabel))
        {
            return "0";
        }

        var parts = ownedLabel.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? parts[1] : ownedLabel;
    }
}
