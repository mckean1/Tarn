using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Dashboard;

public static class DashboardRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var model = state.Dashboard.ViewModel;
        if (model is null)
        {
            return ScreenText.EmptyState("Dashboard", "Dashboard data is unavailable.", body.Width);
        }

        return body.Width >= 76 && body.Height >= 14
            ? RenderTwoColumns(model, state.Dashboard.SelectedActionIndex, body)
            : RenderStacked(model, state.Dashboard.SelectedActionIndex, body);
    }

    private static string RenderTwoColumns(Tarn.ClientApp.Play.Queries.DashboardViewModel model, int selectedActionIndex, Rect body)
    {
        const int gap = 2;
        var seasonLines = BuildSeasonStatusLines(model);
        var nextMatchLines = BuildNextMatchLines(model);
        var leftWidth = Math.Max(28, ((body.Width - gap) * 9) / 20);
        var topHeight = Math.Clamp(Math.Max(5, Math.Max(seasonLines.Count, nextMatchLines.Count) + 2), 5, Math.Max(5, body.Height - 4));
        var (topRect, bottomRect) = body.SplitRows(topHeight);
        var (topLeft, topRight) = topRect.SplitColumns(leftWidth, gap);
        var (bottomLeft, bottomRight) = bottomRect.SplitColumns(topLeft.Width, gap);
        var recentLines = BuildRecentActivityLines(model, Math.Max(1, bottomLeft.GetInnerRect().Height));
        var actionLines = BuildActionLines(model, selectedActionIndex, Math.Max(1, bottomRight.GetInnerRect().Height));

        var topLines = BoxDrawing.MergeColumns(
            BoxDrawing.RenderBox("Season Status", seasonLines, topLeft.Width, topLeft.Height),
            BoxDrawing.RenderBox("Next Match", nextMatchLines, topRight.Width, topRight.Height),
            gap);
        var bottomLines = BoxDrawing.MergeColumns(
            BoxDrawing.RenderBox("Recent Activity", recentLines, bottomLeft.Width, bottomLeft.Height),
            BoxDrawing.RenderBox("Recommended Actions", actionLines, bottomRight.Width, bottomRight.Height),
            gap);

        return string.Join(Environment.NewLine, topLines.Concat(bottomLines));
    }

    private static string RenderStacked(Tarn.ClientApp.Play.Queries.DashboardViewModel model, int selectedActionIndex, Rect body)
    {
        var sections = new (string Title, IReadOnlyList<string> Lines)[]
        {
            ("Season Status", BuildSeasonStatusLines(model)),
            ("Next Match", BuildNextMatchLines(model)),
            ("Recent Activity", BuildRecentActivityLines(model, 3)),
            ("Recommended Actions", BuildActionLines(model, selectedActionIndex, Math.Max(2, Math.Min(5, body.Height / 3)))),
        };

        var lines = new List<string>();
        for (var index = 0; index < sections.Length; index++)
        {
            var remainingHeight = body.Height - lines.Count;
            if (remainingHeight < 3)
            {
                break;
            }

            var minimumHeightAfter = Math.Max(0, (sections.Length - index - 1) * 3);
            var desiredHeight = sections[index].Lines.Count + 2;
            var sectionHeight = Math.Clamp(desiredHeight, 3, Math.Max(3, remainingHeight - minimumHeightAfter));
            lines.AddRange(BoxDrawing.RenderBox(sections[index].Title, sections[index].Lines, body.Width, sectionHeight));
        }

        return string.Join(Environment.NewLine, lines.Take(body.Height));
    }

    private static IReadOnlyList<string> BuildSeasonStatusLines(Tarn.ClientApp.Play.Queries.DashboardViewModel model) =>
    [
        $"Record: {model.Record}",
        $"Rank: {model.Rank}",
        $"Deck: {ScreenText.StatusChip(model.DeckLegality)} {model.DeckSize}",
    ];

    private static IReadOnlyList<string> BuildNextMatchLines(Tarn.ClientApp.Play.Queries.DashboardViewModel model)
    {
        if (model.NextMatch is null)
        {
            return
            [
                "Opponent: None",
                $"League: {model.League}",
                "Status: No fixture",
            ];
        }

        return
        [
            $"Opponent: {model.NextMatch.Opponent}",
            $"League: {model.NextMatch.League}",
            $"Status: {model.NextMatch.Status}",
        ];
    }

    private static IReadOnlyList<string> BuildRecentActivityLines(Tarn.ClientApp.Play.Queries.DashboardViewModel model, int maxLines) =>
        model.RecentActivity.Take(Math.Max(1, maxLines)).ToList();

    private static IReadOnlyList<string> BuildActionLines(Tarn.ClientApp.Play.Queries.DashboardViewModel model, int selectedActionIndex, int maxLines)
    {
        var lineCount = Math.Max(1, maxLines);
        return model.RecommendedActions
            .Take(lineCount)
            .Select((action, index) => ScreenText.InteractiveRow(index == selectedActionIndex, action))
            .ToList();
    }
}
