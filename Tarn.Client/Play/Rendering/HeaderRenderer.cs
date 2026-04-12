using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public static class HeaderRenderer
{
    public static string Render(AppState state, int width)
    {
        var player = state.HumanPlayer;
        var screen = PlayScreenCatalog.Get(state.ActiveScreen);
        var viewedWeek = state.ActiveScreen == ScreenId.Schedule && state.Schedule.SelectedWeek > 0
            ? state.Schedule.SelectedWeek
            : state.World.Season.CurrentWeek;
        var segments = new[]
        {
            screen.Title,
            $"Year {state.World.Season.Year} Week {viewedWeek}",
            player.League.ToString(),
            $"Cash {player.Cash}",
        };
        return Layout.Truncate(string.Join(" · ", segments), width);
    }
}
