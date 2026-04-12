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
            return "Week Summary unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(summary.Title);
        builder.AppendLine(ScreenText.Divider(body.Width - 1));
        foreach (var line in summary.Lines)
        {
            builder.AppendLine(line);
        }
        builder.AppendLine();
        builder.AppendLine("Actions");
        for (var index = 0; index < summary.Actions.Count; index++)
        {
            var marker = index == state.WeekSummary.SelectedActionIndex ? ">" : " ";
            builder.AppendLine($"{marker} {summary.Actions[index]}");
        }

        return ScreenText.FitBlock(builder.ToString(), body.Width, body.Height);
    }
}
