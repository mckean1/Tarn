using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public static class FooterRenderer
{
    public static string Render(ScreenId screen, int width)
    {
        var controls = PlayScreenCatalog.Get(screen).ControlsText;
        var line1 = Layout.Truncate(controls, width);
        var line2Text = width < 56
            ? string.Empty
            : string.Join(" · ", PlayScreenCatalog.GetGlobalNavigationEntries(compact: true));
        var line2 = string.IsNullOrEmpty(line2Text)
            ? string.Empty
            : ScreenText.Secondary(Layout.Truncate($"Screens {line2Text}", width));
        return string.Join(Environment.NewLine, [line1, line2]);
    }
}
