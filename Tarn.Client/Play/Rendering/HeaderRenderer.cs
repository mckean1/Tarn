using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public static class HeaderRenderer
{
    public static string Render(AppState state, int width)
    {
        var player = state.HumanPlayer;
        var line1 = Layout.Truncate(state.IsNarrowLayout ? "Tarn Play [Compact]" : "Tarn Play", width);
        var line2 = Layout.Truncate($"Year {state.World.Season.Year}, Week {state.World.Season.CurrentWeek} | {player.Name} | {player.League}", width);
        var line3 = Layout.Truncate(
            PlayScreenCatalog.BuildGlobalNavigationText(
                state.IsNarrowLayout,
                state.IsNarrowLayout ? $"Cash {player.Cash} | " : $"Cash {player.Cash} | Screens: "),
            width);
        return string.Join(Environment.NewLine, [line1, line2, line3]);
    }
}
