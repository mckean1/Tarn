using System.Text;
using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public sealed class AppRenderer
{
    public string Render(AppState state)
    {
        const int chromeLines = 5;
        var frameWidth = Math.Max(40, state.WindowWidth);
        var innerWidth = Math.Max(38, frameWidth - 2);
        var layout = new Layout(innerWidth, Math.Max(12, state.WindowHeight - chromeLines));
        var builder = new StringBuilder();

        builder.AppendLine(BoxDrawing.TopBorder(layout.Width, "TARN"));
        AppendSection(builder, HeaderRenderer.Render(state, layout.Header.Width), layout.Header.Width, layout.Header.Height);
        builder.AppendLine(BoxDrawing.Divider(layout.Width));
        AppendSection(builder, RenderBody(state, layout.Body), layout.Body.Width, layout.Body.Height);
        builder.AppendLine(BoxDrawing.Divider(layout.Width));
        AppendSection(builder, MessageBarRenderer.Render(state.MessageBar, layout.MessageBar.Width), layout.MessageBar.Width, layout.MessageBar.Height);
        builder.AppendLine(BoxDrawing.Divider(layout.Width));
        AppendSection(builder, FooterRenderer.Render(state.ActiveScreen, layout.Footer.Width), layout.Footer.Width, layout.Footer.Height);
        builder.Append(BoxDrawing.BottomBorder(layout.Width));

        if (state.Modal is not null)
        {
            builder.AppendLine();
            builder.Append(ModalRenderer.Render(state.Modal, state.WindowWidth));
        }

        return FrameNormalizer.Normalize(builder.ToString(), state.WindowWidth, state.WindowHeight);
    }

    private static string RenderBody(AppState state, Rect body)
    {
        return PlayScreenCatalog.Get(state.ActiveScreen).Render(state, body);
    }

    private static void AppendSection(StringBuilder builder, string content, int width, int height)
    {
        var lines = content.Split(Environment.NewLine, StringSplitOptions.None);
        for (var index = 0; index < height; index++)
        {
            builder.AppendLine(BoxDrawing.FrameLine(index < lines.Length ? lines[index] : string.Empty, width));
        }
    }
}
