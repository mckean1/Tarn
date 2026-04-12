using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Collection;

public static class CollectionRenderer
{
    private const string ColumnSeparator = "  ";

    public static string Render(AppState state, Rect body)
    {
        var model = state.Collection.ViewModel;
        if (model is null)
        {
            return "Collection unavailable.";
        }

        return body.Width >= 86 && body.Height >= 12
            ? RenderTwoColumns(model, body)
            : RenderStacked(model, body);
    }

    private static string RenderTwoColumns(CollectionViewModel model, Rect body)
    {
        const int gap = 2;
        var leftWidth = Math.Max(42, ((body.Width - gap) * 11) / 20);
        var (left, right) = body.SplitColumns(leftWidth, gap);
        var browseBox = BoxDrawing.RenderBox("Collection", BuildBrowsePaneLines(model, left.GetInnerRect()), left.Width, left.Height);
        var detailBox = BoxDrawing.RenderBox("Selected Card", BuildDetailPaneLines(model.Detail, right.GetInnerRect()), right.Width, right.Height);
        return string.Join(Environment.NewLine, BoxDrawing.MergeColumns(browseBox, detailBox, gap));
    }

    private static string RenderStacked(CollectionViewModel model, Rect body)
    {
        var topHeight = Math.Max(7, body.Height / 2);
        var (top, bottom) = body.SplitRows(topHeight, 1);
        var lines = BoxDrawing.RenderBox("Collection", BuildBrowsePaneLines(model, top.GetInnerRect()), top.Width, top.Height)
            .Concat(BoxDrawing.RenderBox("Selected Card", BuildDetailPaneLines(model.Detail, bottom.GetInnerRect()), bottom.Width, bottom.Height));
        return string.Join(Environment.NewLine, lines.Take(body.Height));
    }

    private static IReadOnlyList<string> BuildBrowsePaneLines(CollectionViewModel model, Rect rect)
    {
        var lines = new List<string>
        {
            Layout.Truncate($"Filter: {model.FilterLabel}", rect.Width),
            Layout.Truncate($"Sort: {model.SortLabel}", rect.Width),
            string.Empty,
        };

        if (model.Rows.Count == 0)
        {
            lines.Add("[No Cards]");
            lines.Add(Layout.Truncate("No cards match this filter.", rect.Width));
            return lines.Take(Math.Max(1, rect.Height)).ToList();
        }

        var columns = BuildColumns(Math.Max(20, rect.Width - 2));
        lines.Add(ScreenText.Secondary(Layout.Truncate($"  {TableRenderer.BuildHeader(columns, ColumnSeparator)}", rect.Width)));
        lines.Add(ScreenText.Secondary(Layout.Truncate($"  {TableRenderer.BuildDivider(columns, ColumnSeparator, '─')}", rect.Width)));

        var visibleRows = Math.Max(1, rect.Height - lines.Count);
        var startIndex = Math.Clamp(model.SelectedIndex - (visibleRows / 2), 0, Math.Max(0, model.Rows.Count - visibleRows));
        var displayedRows = Math.Min(model.Rows.Count - startIndex, visibleRows);
        for (var index = 0; index < displayedRows; index++)
        {
            var rowIndex = startIndex + index;
            var row = model.Rows[rowIndex];
            var content = TableRenderer.BuildRow(
                [row.DisplayName, row.TypeLabel, row.RarityLabel, row.OwnedCount.ToString()],
                columns,
                ColumnSeparator);
            lines.Add(Layout.Truncate(ScreenText.InteractiveRow(rowIndex == model.SelectedIndex, content), rect.Width));
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildDetailPaneLines(CollectionDetailViewModel? detail, Rect rect)
    {
        if (detail is null)
        {
            return
            [
                "No card selected.",
                Layout.Truncate("Adjust the filter or browse the list.", rect.Width),
            ];
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

        lines.Add(Layout.Truncate($"Owned: {detail.OwnedCount}", rect.Width));
        lines.Add(Layout.Truncate($"In active deck: {detail.UsedInDeckCount}", rect.Width));
        lines.Add(Layout.Truncate($"Keywords: {detail.KeywordsText}", rect.Width));
        lines.Add(string.Empty);
        lines.Add(ScreenText.Secondary("Rules"));
        lines.AddRange(ScreenText.WrapLines(detail.RulesText, rect.Width));
        return lines.Take(Math.Max(1, rect.Height)).ToList();
    }

    private static IReadOnlyList<TableColumn> BuildColumns(int availableWidth)
    {
        var widths = new[] { 24, 10, 10, 5 };
        var minimums = new[] { 12, 8, 8, 5 };
        var targetWidth = widths.Sum() + (ColumnSeparator.Length * 3);
        if (availableWidth >= targetWidth)
        {
            widths[0] += availableWidth - targetWidth;
        }
        else
        {
            var overflow = targetWidth - availableWidth;
            foreach (var index in new[] { 0, 2, 1 })
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

        return
        [
            new TableColumn { Header = "Name", Width = widths[0] },
            new TableColumn { Header = "Type", Width = widths[1] },
            new TableColumn { Header = "Rarity", Width = widths[2] },
            new TableColumn { Header = "Owned", Width = widths[3], Alignment = TableCellAlignment.Right },
        ];
    }
}
