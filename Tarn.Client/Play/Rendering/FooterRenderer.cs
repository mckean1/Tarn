using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public static class FooterRenderer
{
    public static string Render(ScreenId screen, int width)
    {
        var controls = PlayScreenCatalog.Get(screen).ControlsText;
        var line1 = Layout.Truncate(controls, width);
        var line2 = Layout.Truncate(
            PlayScreenCatalog.BuildGlobalNavigationText(width < 90, width < 90 ? string.Empty : "Global nav: "),
            width);
        return string.Join(Environment.NewLine, [line1, line2]);
    }
}
