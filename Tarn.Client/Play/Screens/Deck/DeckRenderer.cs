using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Deck;

public static class DeckRenderer
{
    private const string ColumnSeparator = "  ";

    public static string Render(AppState state, Rect body)
    {
        var model = state.Deck.ViewModel;
        if (model is null)
        {
            return "Deck unavailable.";
        }

        return body.Width >= 88 && body.Height >= 14
            ? RenderTwoColumns(state, model, body)
            : RenderStacked(state, model, body);
    }

    private static string RenderTwoColumns(AppState state, DeckViewModel model, Rect body)
    {
        var summaryHeight = Math.Min(6, Math.Max(4, body.Height / 3));
        var (summaryRect, contentRect) = body.SplitRows(summaryHeight, 1);
        const int gap = 2;
        var leftWidth = Math.Max(38, ((contentRect.Width - gap) * 11) / 20);
        var (left, right) = contentRect.SplitColumns(leftWidth, gap);

        var summaryBox = BoxDrawing.RenderBox("Deck Summary", BuildSummaryLines(model, summaryRect.GetInnerRect()), summaryRect.Width, summaryRect.Height);
        var browseBox = BoxDrawing.RenderBox("Deck Contents", BuildBrowsePaneLines(state, model, left.GetInnerRect()), left.Width, left.Height);
        var detailBox = BoxDrawing.RenderBox("Selected Card", BuildDetailPaneLines(model.Detail, right.GetInnerRect()), right.Width, right.Height);

        return string.Join(Environment.NewLine, summaryBox.Concat(BoxDrawing.MergeColumns(browseBox, detailBox, gap)).Take(body.Height));
    }

    private static string RenderStacked(AppState state, DeckViewModel model, Rect body)
    {
        var summaryHeight = Math.Min(6, Math.Max(4, body.Height / 4));
        var remainingHeight = Math.Max(3, body.Height - summaryHeight - 1);
        var (summaryRect, contentRect) = body.SplitRows(summaryHeight, 1);
        var detailHeight = Math.Max(5, remainingHeight / 2);
        var browseHeight = Math.Max(3, contentRect.Height - detailHeight - 1);
        var (browseRect, detailRect) = contentRect.SplitRows(browseHeight, 1);

        var lines = BoxDrawing.RenderBox("Deck Summary", BuildSummaryLines(model, summaryRect.GetInnerRect()), summaryRect.Width, summaryRect.Height)
            .Concat(BoxDrawing.RenderBox("Deck Contents", BuildBrowsePaneLines(state, model, browseRect.GetInnerRect()), browseRect.Width, browseRect.Height))
            .Concat(BoxDrawing.RenderBox("Selected Card", BuildDetailPaneLines(model.Detail, detailRect.GetInnerRect()), detailRect.Width, detailRect.Height));

        return string.Join(Environment.NewLine, lines.Take(body.Height));
    }

    private static IReadOnlyList<string> BuildSummaryLines(DeckViewModel model, Rect rect)
    {
        return
        [
            Layout.Truncate($"Deck {model.LegalitySummary}", rect.Width),
            Layout.Truncate($"{model.TotalCards} · {model.PowerSummary}", rect.Width),
            Layout.Truncate(model.TypeSummary, rect.Width),
            Layout.Truncate($"Champion: {model.ChampionName}", rect.Width),
        ];
    }

    private static IReadOnlyList<string> BuildBrowsePaneLines(AppState state, DeckViewModel model, Rect rect)
    {
        var columns = BuildColumns(Math.Max(20, rect.Width - 2));
        var lines = new List<string>
        {
            ScreenText.Secondary(Layout.Truncate($"  {TableRenderer.BuildHeader(columns, ColumnSeparator)}", rect.Width)),
            ScreenText.Secondary(Layout.Truncate($"  {TableRenderer.BuildDivider(columns, ColumnSeparator, '─')}", rect.Width)),
        };

        var contentLines = BuildBrowseLines(state, model, columns, rect.Width);
        if (contentLines.Count == 0)
        {
            contentLines.Add(new DeckBrowseLine("[No Cards]", false, false));
            contentLines.Add(new DeckBrowseLine("No active deck entries available.", false, false));
        }

        var availableRows = Math.Max(1, rect.Height - lines.Count);
        var selectedLineIndex = contentLines.FindIndex(line => line.IsSelected);
        var startIndex = Math.Clamp(selectedLineIndex - (availableRows / 2), 0, Math.Max(0, contentLines.Count - availableRows));
        while (startIndex > 0 && !contentLines[startIndex].IsGroupHeader)
        {
            startIndex--;
        }

        lines.AddRange(contentLines.Skip(startIndex).Take(availableRows).Select(line => Layout.Truncate(line.Text, rect.Width)));
        return lines;
    }

    private static List<DeckBrowseLine> BuildBrowseLines(AppState state, DeckViewModel model, IReadOnlyList<TableColumn> columns, int width)
    {
        var lines = new List<DeckBrowseLine>();
        foreach (var group in model.Groups)
        {
            lines.Add(new DeckBrowseLine(ScreenText.Secondary($"{group.Name} ({group.Entries.Sum(entry => entry.CopiesInDeck)})"), false, true));
            if (group.Entries.Count == 0)
            {
                lines.Add(new DeckBrowseLine($"  {group.EmptyState}", false, false));
            }
            else
            {
                foreach (var entry in group.Entries)
                {
                    var rowText = TableRenderer.BuildRow(
                        [entry.Name, entry.Rarity, $"x{entry.CopiesInDeck}"],
                        columns,
                        ColumnSeparator);
                    lines.Add(new DeckBrowseLine(ScreenText.InteractiveRow(model.Entries[state.Deck.SelectedIndex].CardId == entry.CardId, rowText), model.Entries[state.Deck.SelectedIndex].CardId == entry.CardId, false));
                }
            }

            if (group != model.Groups[^1])
            {
                lines.Add(new DeckBrowseLine(string.Empty, false, false));
            }
        }

        if (lines.Count > 0 && string.IsNullOrEmpty(lines[^1].Text))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildDetailPaneLines(DeckDetailViewModel? detail, Rect rect)
    {
        if (detail is null)
        {
            return
            [
                "No card selected.",
                Layout.Truncate("Your active deck does not have a card to inspect.", rect.Width),
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

        lines.Add(Layout.Truncate($"In deck: {detail.CopiesInDeck}", rect.Width));
        lines.Add(Layout.Truncate($"Owned: {detail.OwnedCount}", rect.Width));
        lines.Add(Layout.Truncate($"Keywords: {detail.KeywordsText}", rect.Width));
        lines.Add(string.Empty);
        lines.Add(ScreenText.Secondary("Rules"));
        lines.AddRange(ScreenText.WrapLines(detail.RulesText, rect.Width));
        return lines.Take(Math.Max(1, rect.Height)).ToList();
    }

    private static IReadOnlyList<TableColumn> BuildColumns(int availableWidth)
    {
        var widths = new[] { 24, 10, 5 };
        var minimums = new[] { 12, 8, 4 };
        var targetWidth = widths.Sum() + (ColumnSeparator.Length * 2);
        if (availableWidth >= targetWidth)
        {
            widths[0] += availableWidth - targetWidth;
        }
        else
        {
            var overflow = targetWidth - availableWidth;
            foreach (var index in new[] { 0, 1 })
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
            new TableColumn { Header = "Rarity", Width = widths[1] },
            new TableColumn { Header = "Qty", Width = widths[2], Alignment = TableCellAlignment.Right },
        ];
    }

    private sealed record DeckBrowseLine(string Text, bool IsSelected, bool IsGroupHeader);
}
