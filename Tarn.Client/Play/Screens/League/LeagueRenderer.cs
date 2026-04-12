using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.League;

public static class LeagueRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var model = state.League.ViewModel;
        if (model is null)
        {
            return "League unavailable.";
        }

        var layout = new Layout(body.Width, body.Height + Layout.HeaderHeight + Layout.MessageBarHeight + Layout.FooterHeight);
        var (left, right) = layout.SplitBodyTwoColumns(Math.Max(46, body.Width / 2));
        var rows = model.Rows.Select((row, index) => new[]
        {
            index == state.League.SelectedIndex ? ">" : row.IsHuman ? "*" : " ",
            row.Rank,
            row.PlayerName + (row.IsHuman ? " (You)" : string.Empty),
            row.Record,
            row.MatchPoints,
            row.GameDiff,
            row.Form,
        } as IReadOnlyList<string>).ToList();
        var table = TableRenderer.Render(
        [
            new TableColumn { Header = "", Width = 1 },
            new TableColumn { Header = "Rk", Width = 3 },
            new TableColumn { Header = "Player", Width = Math.Max(10, left.Width - 28) },
            new TableColumn { Header = "W-L", Width = 5 },
            new TableColumn { Header = "Pts", Width = 4 },
            new TableColumn { Header = "GD", Width = 4 },
            new TableColumn { Header = "Form", Width = 5 },
        ], rows);

        var detailLines = new List<string>
        {
            $"League: {model.LeagueName}",
            $"Focus: {model.Detail.PlayerName}",
            $"Streak: {model.Detail.Streak}",
            $"Behind leader: {model.Detail.PointsBehindLeader}",
            $"Rivals: {model.Detail.RivalGap}",
            "Recent results:",
        };
        detailLines.AddRange(model.Detail.RecentResults.Select(result => $"- {result}"));

        return ScreenText.FitLines(
            table.Split(Environment.NewLine)
                .Concat([""])
                .Concat(detailLines),
            body.Width,
            body.Height);
    }
}
