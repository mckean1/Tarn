using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Collection;

public static class CollectionRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var model = state.Collection.ViewModel;
        if (model is null)
        {
            return "Collection unavailable.";
        }

        var lines = new List<string>
        {
            $"Collection | Filter: {model.Filter} | Sort: {model.Sort}",
            "Left/Right changes filter. Enter changes sort.",
        };
        var rows = model.Rows.Select((row, index) =>
            $"{(index == state.Collection.SelectedIndex ? ">" : " ")} {Layout.Truncate(row.Name, 24)} {row.Type,-9} {row.Rarity,-10} {row.OwnedCount,2}").ToList();
        if (rows.Count == 0)
        {
            rows.Add(ScreenText.EmptyState("Collection", "No cards match this filter.", body.Width));
        }

        lines.AddRange(rows.Take(Math.Max(1, body.Height - 8)));
        if (model.Detail is not null)
        {
            lines.Add(string.Empty);
            lines.Add($"Detail: {model.Detail.Name}");
            lines.Add(model.Detail.StatsText);
            lines.Add($"Owned: {model.Detail.OwnedCount} | In active deck: {model.Detail.UsedInDeckCount}");
            lines.Add(model.Detail.RulesText);
        }

        return ScreenText.FitLines(lines, body.Width, body.Height);
    }
}
