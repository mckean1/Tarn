using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Schedule;

public static class ScheduleRenderer
{
    public static string Render(AppState state, Rect body)
    {
        return body.Width >= 78 && body.Height >= 12
            ? RenderTwoColumns(state, body)
            : RenderStacked(state, body);
    }

    private static string RenderTwoColumns(AppState state, Rect body)
    {
        const int gap = 2;
        var leftWidth = Math.Max(38, ((body.Width - gap) * 3) / 5);
        var (left, right) = body.SplitColumns(leftWidth, gap);
        var fixtureBox = BoxDrawing.RenderBox($"Fixtures · Week {state.Schedule.SelectedWeek}", BuildFixturePaneLines(state, left.GetInnerRect()), left.Width, left.Height);
        var detailBox = BoxDrawing.RenderBox(state.Schedule.Detail?.Title ?? "Selected Fixture", BuildDetailPaneLines(state, right.GetInnerRect()), right.Width, right.Height);
        return string.Join(Environment.NewLine, BoxDrawing.MergeColumns(fixtureBox, detailBox, gap));
    }

    private static string RenderStacked(AppState state, Rect body)
    {
        var topHeight = Math.Max(6, body.Height / 2);
        var (top, bottom) = body.SplitRows(topHeight, 1);
        var lines = BoxDrawing.RenderBox($"Fixtures · Week {state.Schedule.SelectedWeek}", BuildFixturePaneLines(state, top.GetInnerRect()), body.Width, top.Height)
            .Concat(BoxDrawing.RenderBox(state.Schedule.Detail?.Title ?? "Selected Fixture", BuildDetailPaneLines(state, bottom.GetInnerRect()), body.Width, bottom.Height));
        return string.Join(Environment.NewLine, lines.Take(body.Height));
    }

    private static IReadOnlyList<string> BuildFixturePaneLines(AppState state, Rect rect)
    {
        var lines = new List<string>();
        var columnWidths = CalculateColumnWidths(rect.Width);

        lines.Add(Layout.Truncate($"{state.Schedule.FocusLeagueLabel} League", rect.Width));
        lines.Add(ScreenText.Secondary(BuildHeaderRow(columnWidths, rect.Width)));
        lines.Add(ScreenText.Secondary(new string('─', Math.Max(8, rect.Width))));

        if (state.Schedule.Fixtures.Count == 0)
        {
            lines.Add(Layout.Truncate($"No {state.Schedule.FocusLeagueLabel.ToLowerInvariant()} fixtures are scheduled for week {state.Schedule.SelectedWeek}.", rect.Width));
            lines.Add(Layout.Truncate("Use ←/→ to browse other weeks.", rect.Width));
            return lines;
        }

        var visibleRows = Math.Max(1, rect.Height - lines.Count);
        var startIndex = Math.Clamp(state.Schedule.SelectedFixtureIndex - (visibleRows / 2), 0, Math.Max(0, state.Schedule.Fixtures.Count - visibleRows));
        var displayedRows = Math.Min(state.Schedule.Fixtures.Count - startIndex, visibleRows);
        for (var index = 0; index < displayedRows; index++)
        {
            var fixtureIndex = startIndex + index;
            lines.Add(BuildFixtureRow(state.Schedule.Fixtures[fixtureIndex], fixtureIndex == state.Schedule.SelectedFixtureIndex, columnWidths, rect.Width));
        }

        if (startIndex + displayedRows < state.Schedule.Fixtures.Count)
        {
            lines[^1] = ScreenText.Secondary(Layout.Truncate($"… {state.Schedule.Fixtures.Count - (startIndex + displayedRows)} more fixtures", rect.Width));
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildDetailPaneLines(AppState state, Rect rect)
    {
        var detail = state.Schedule.Detail;
        if (detail is null)
        {
            return ["No fixture selected."];
        }

        var lines = detail.Lines
            .Select(line => Layout.Truncate(line, rect.Width))
            .ToList();
        lines.Add(string.Empty);
        lines.Add(ScreenText.Secondary("Controls"));
        lines.Add(Layout.Truncate(detail.ReplayAvailable ? "Enter: Open replay" : "Enter: Replay unavailable", rect.Width));
        lines.Add(Layout.Truncate("←/→: Change week", rect.Width));
        lines.Add(Layout.Truncate("Esc: Back", rect.Width));
        lines.Add(Layout.Truncate("?: Help", rect.Width));
        return lines;
    }

    private static string BuildFixtureRow(ScheduleFixtureItem fixture, bool isSelected, (int Pairing, int Status, int Result) widths, int totalWidth)
    {
        var content = string.Join(
            "  ",
            TextLayout.TruncateVisible(fixture.Pairing, widths.Pairing),
            TextLayout.TruncateVisible(fixture.Status, widths.Status),
            TextLayout.TruncateVisible(fixture.Result, widths.Result));
        return Layout.Truncate(ScreenText.InteractiveRow(isSelected, content), totalWidth);
    }

    private static string BuildHeaderRow((int Pairing, int Status, int Result) widths, int totalWidth)
    {
        var content = string.Join(
            "  ",
            TextLayout.TruncateVisible("Pairing", widths.Pairing),
            TextLayout.TruncateVisible("State", widths.Status),
            TextLayout.TruncateVisible("Result", widths.Result));
        return Layout.Truncate($"  {content}", totalWidth);
    }

    private static (int Pairing, int Status, int Result) CalculateColumnWidths(int totalWidth)
    {
        var contentWidth = Math.Max(18, totalWidth - 2);
        var statusWidth = Math.Clamp(contentWidth / 5, 7, 10);
        var resultWidth = Math.Clamp(contentWidth / 8, 3, 6);
        var pairingWidth = Math.Max(8, contentWidth - statusWidth - resultWidth - 4);
        return (pairingWidth, statusWidth, resultWidth);
    }
}
