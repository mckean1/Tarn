using System.Text;
using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.WeekSummary;

public static class WeekSummaryRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var summary = state.WeekSummary.Summary;
        if (summary is null)
        {
            return ScreenText.FitBlock("Week Summary unavailable.", body.Width, body.Height);
        }

        return summary.IsGenerated
            ? RenderGeneratedState(summary, state.WeekSummary.SelectedActionIndex, body)
            : RenderEmptyState(summary, state.WeekSummary.SelectedActionIndex, body);
    }

    private static string RenderGeneratedState(WeekSummaryViewModel summary, int selectedActionIndex, Rect body)
    {
        if (body.Width >= 74 && body.Height >= 12)
        {
            const int gap = 2;
            var summaryHeight = Math.Clamp(summary.Highlights.Count + 4, 6, Math.Max(6, body.Height - 4));
            var (top, bottom) = body.SplitRows(summaryHeight);
            var actionWidth = Math.Clamp(body.Width / 3, 24, Math.Max(24, body.Width - 22));
            var detailWidth = Math.Max(12, bottom.Width - actionWidth - gap);
            var (details, actions) = bottom.SplitColumns(detailWidth, gap);
            var topLines = BoxDrawing.RenderBox(summary.Title, BuildPrimaryLines(summary), top.Width, top.Height);
            var bottomLines = BoxDrawing.MergeColumns(
                BoxDrawing.RenderBox("Notable Updates", BuildNoteLines(summary), details.Width, details.Height),
                BoxDrawing.RenderBox("Actions", BuildActionLines(summary, selectedActionIndex), actions.Width, actions.Height),
                gap);
            return string.Join(Environment.NewLine, topLines.Concat(bottomLines).Take(body.Height));
        }

        var sections = new List<string>();
        sections.AddRange(BoxDrawing.RenderBox(summary.Title, BuildPrimaryLines(summary), body.Width, Math.Min(body.Height, Math.Max(6, summary.Highlights.Count + 4))));
        if (sections.Count < body.Height)
        {
            sections.AddRange(BoxDrawing.RenderBox("Notable Updates", BuildNoteLines(summary), body.Width, Math.Max(3, Math.Min(body.Height - sections.Count, summary.Notes.Count + 2))));
        }

        if (sections.Count < body.Height)
        {
            sections.AddRange(BoxDrawing.RenderBox("Actions", BuildActionLines(summary, selectedActionIndex), body.Width, Math.Max(3, Math.Min(body.Height - sections.Count, summary.Actions.Count + 2))));
        }

        return string.Join(Environment.NewLine, sections.Take(body.Height));
    }

    private static string RenderEmptyState(WeekSummaryViewModel summary, int selectedActionIndex, Rect body)
    {
        var panelWidth = Math.Clamp(Math.Min(body.Width, 64), 24, Math.Max(24, body.Width));
        var actionWidth = Math.Clamp(Math.Min(body.Width, 36), 20, Math.Max(20, body.Width));
        var summaryLines = CenterLines(BoxDrawing.RenderBox(summary.Title, BuildPrimaryLines(summary), panelWidth, Math.Max(6, summary.Highlights.Count + 4)), body.Width);
        var actionLines = CenterLines(BoxDrawing.RenderBox("Actions", BuildActionLines(summary, selectedActionIndex), actionWidth, Math.Max(3, summary.Actions.Count + 2)), body.Width);
        var content = summaryLines
            .Concat([string.Empty])
            .Concat(actionLines)
            .ToList();
        var topPadding = Math.Max(0, (body.Height - content.Count) / 3);
        var lines = Enumerable.Repeat(string.Empty, topPadding)
            .Concat(content)
            .ToList();
        return ScreenText.FitLines(lines, body.Width, body.Height);
    }

    private static IReadOnlyList<string> BuildPrimaryLines(WeekSummaryViewModel summary)
    {
        var lines = new List<string>
        {
            ScreenText.Secondary(summary.Subtitle),
            string.Empty,
        };

        lines.AddRange(summary.Highlights);
        return lines;
    }

    private static IReadOnlyList<string> BuildNoteLines(WeekSummaryViewModel summary)
        => summary.Notes.Count == 0 ? ["No additional updates recorded."] : summary.Notes;

    private static IReadOnlyList<string> BuildActionLines(WeekSummaryViewModel summary, int selectedActionIndex)
        => summary.Actions
            .Select((action, index) => ScreenText.InteractiveRow(index == selectedActionIndex, action))
            .ToList();

    private static IReadOnlyList<string> CenterLines(IReadOnlyList<string> lines, int width)
    {
        var lineWidth = lines.Count == 0 ? 0 : lines.Max(AnsiUtility.GetVisibleLength);
        var leftPadding = Math.Max(0, (width - lineWidth) / 2);
        var prefix = new string(' ', leftPadding);
        return lines.Select(line => prefix + line).ToList();
    }
}
