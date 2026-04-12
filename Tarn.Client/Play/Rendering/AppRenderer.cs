using System.Text;
using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public sealed class AppRenderer
{
    public string Render(AppState state)
    {
        var layout = new Layout(state.WindowWidth, state.WindowHeight);
        var builder = new StringBuilder();
        builder.AppendLine(HeaderRenderer.Render(state, layout.Header.Width));
        builder.AppendLine(RenderBody(state, layout.Body));
        builder.AppendLine(MessageBarRenderer.Render(state.MessageBar, layout.MessageBar.Width));
        builder.Append(FooterRenderer.Render(state.ActiveScreen, layout.Footer.Width));

        if (state.Modal is not null)
        {
            builder.AppendLine();
            builder.Append(ModalRenderer.Render(state.Modal, state.WindowWidth));
        }

        return builder.ToString();
    }

    private static string RenderBody(AppState state, Rect body)
    {
        return PlayScreenCatalog.Get(state.ActiveScreen).Render(state, body);
    }
}
