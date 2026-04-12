using System.Text;
using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Schedule;

public static class ScheduleRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var layout = new Layout(body.Width, body.Height + Layout.HeaderHeight + Layout.MessageBarHeight + Layout.FooterHeight);
        var mode = layout.ChooseColumns();
        var (left, right) = layout.SplitBodyTwoColumns(mode == LayoutMode.SingleColumn ? body.Width : Math.Max(24, body.Width / 2));
        var builder = new StringBuilder();
        builder.AppendLine($"Schedule - Week {state.Schedule.SelectedWeek}");
        builder.AppendLine(ScreenText.Divider(body.Width - 1));

        if (state.Schedule.Fixtures.Count == 0)
        {
            builder.AppendLine(ScreenText.EmptyState("No Fixtures", "No matches are scheduled for this week window.", body.Width));
        }
        else
        {
            builder.AppendLine("Fixtures");
            var visibleRows = mode == LayoutMode.SingleColumn ? Math.Max(4, body.Height / 2) : left.Height - 4;
            for (var index = 0; index < state.Schedule.Fixtures.Count && index < visibleRows; index++)
            {
                var marker = index == state.Schedule.SelectedFixtureIndex ? ">" : " ";
                builder.AppendLine($"{marker} {Layout.Truncate(state.Schedule.Fixtures[index].Summary, Math.Max(20, left.Width - 2))}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(state.Schedule.Detail?.Title ?? "Detail");
        foreach (var line in state.Schedule.Detail?.Lines ?? ["No fixture selected."])
        {
            builder.AppendLine(Layout.Truncate(line, mode == LayoutMode.SingleColumn ? body.Width : right.Width));
        }

        builder.AppendLine(state.Schedule.Detail?.ReplayAvailable == true ? $"{ScreenText.StatusChip("Replay Ready")} Press Enter" : ScreenText.StatusChip("No Replay"));
        return ScreenText.FitBlock(builder.ToString(), body.Width, body.Height);
    }
}
