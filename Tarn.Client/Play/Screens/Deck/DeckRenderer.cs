using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Deck;

public static class DeckRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var model = state.Deck.ViewModel;
        if (model is null)
        {
            return "Deck unavailable.";
        }

        var lines = new List<string>
        {
            $"Deck {model.LegalitySummary}",
            $"{model.TotalCards} | {model.PowerSummary}",
            $"{model.TypeSummary} | Champion: {model.ChampionName}",
            "Enter auto-builds the best legal deck.",
            string.Empty,
        };

        string? currentGroup = null;
        for (var index = 0; index < model.Entries.Count && lines.Count < body.Height - 4; index++)
        {
            var entry = model.Entries[index];
            if (entry.Group != currentGroup)
            {
                currentGroup = entry.Group;
                lines.Add(currentGroup);
            }

            lines.Add($"{(index == state.Deck.SelectedIndex ? ">" : " ")} {entry.Name} [{entry.Rarity}]");
        }

        if (model.Detail is not null)
        {
            lines.Add(string.Empty);
            lines.Add($"Detail: {model.Detail.Name}");
            lines.Add($"{model.Detail.Type} | ATK {model.Detail.Attack} | HP {model.Detail.Health} | SPD {model.Detail.Speed}");
            lines.Add(model.Detail.RulesText);
        }

        return ScreenText.FitLines(lines, body.Width, body.Height);
    }
}
