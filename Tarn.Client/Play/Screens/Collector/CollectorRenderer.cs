using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Collector;

public static class CollectorRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var model = state.Collector.ViewModel;
        if (model is null)
        {
            return "Collector unavailable.";
        }

        var lines = new List<string>
        {
            $"Collector | Tab: {model.Tab}",
            "Left/Right tabs, Up/Down selection, Enter confirm action.",
        };
        if (model.Rows.Count == 0)
        {
            lines.Add(ScreenText.EmptyState(
                "Collector",
                model.Tab switch
                {
                    CollectorTab.Singles => "The Collector has no singles right now.",
                    CollectorTab.Packs => "No packs are on the shelf today.",
                    _ => "You have nothing ready to sell.",
                },
                body.Width));
        }
        else
        {
            lines.AddRange(model.Rows.Take(Math.Max(1, body.Height - 8)).Select((row, index) =>
                ScreenText.InteractiveRow(
                    index == state.Collector.SelectedIndex,
                    $"{Layout.Truncate(row.Name, 22).TrimEnd()} {row.Type,-10} {row.Rarity,-10} {row.Price,4} {row.Status}")));
        }

        if (model.Detail is not null)
        {
            lines.Add(string.Empty);
            lines.Add($"Detail: {model.Detail.Name}");
            lines.Add(model.Detail.StatsText);
            lines.Add(model.Detail.RulesText);
            lines.Add(model.Detail.PriceLabel);
            lines.Add(model.Detail.ImpactText);
        }

        return ScreenText.FitLines(lines, body.Width, body.Height);
    }
}
