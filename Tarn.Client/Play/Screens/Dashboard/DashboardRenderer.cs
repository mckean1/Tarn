using System.Text;
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
            return "Dashboard unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Dashboard");
        builder.AppendLine($"Year {model.Year}, Week {model.Week} | {model.League} | {model.RankLabel}");
        builder.AppendLine($"Record {model.Record} | Cash {model.Cash}");
        builder.AppendLine($"Deck: {model.DeckLegality} | {model.DeckSize}");
        builder.AppendLine($"Next match: {model.NextMatchSummary}");
        builder.AppendLine();
        builder.AppendLine("Recent activity");
        foreach (var line in model.RecentActivity)
        {
            builder.AppendLine($"- {line}");
        }
        builder.AppendLine();
        builder.AppendLine("Recommended actions");
        for (var index = 0; index < model.RecommendedActions.Count; index++)
        {
            var marker = index == state.Dashboard.SelectedActionIndex ? ">" : " ";
            builder.AppendLine($"{marker} {model.RecommendedActions[index]}");
        }

        return ScreenText.FitBlock(builder.ToString(), body.Width, body.Height);
    }
}
