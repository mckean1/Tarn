using System.Text;
using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public sealed class AppRenderer
{
    public string Render(AppState state)
    {
        const int chromeLines = 5;
        var drawableWidth = FrameNormalizer.GetDrawableWidth(state.WindowWidth);
        var drawableHeight = FrameNormalizer.GetDrawableHeight(state.WindowHeight);
        if (drawableWidth == 0 || drawableHeight == 0)
        {
            return string.Empty;
        }

        var innerWidth = Math.Max(0, drawableWidth - 2);
        var layout = new Layout(innerWidth, Math.Max(0, drawableHeight - chromeLines));
        var builder = new StringBuilder();

        builder.AppendLine(BoxDrawing.TopBorder(layout.Width, "TARN"));
        AppendSection(builder, HeaderRenderer.Render(state, layout.Header.Width), layout.Header.Width, layout.Header.Height);
        builder.AppendLine(BoxDrawing.Divider(layout.Width));
        
        if (state.Modal is not null)
        {
            AppendSection(builder, RenderBodyWithModal(state, layout.Body, drawableWidth), layout.Body.Width, layout.Body.Height);
        }
        else
        {
            AppendSection(builder, RenderBody(state, layout.Body), layout.Body.Width, layout.Body.Height);
        }
        
        builder.AppendLine(BoxDrawing.Divider(layout.Width));
        AppendSection(builder, MessageBarRenderer.Render(state.MessageBar, layout.MessageBar.Width), layout.MessageBar.Width, layout.MessageBar.Height);
        builder.AppendLine(BoxDrawing.Divider(layout.Width));
        AppendSection(builder, FooterRenderer.Render(state.ActiveScreen, layout.Footer.Width), layout.Footer.Width, layout.Footer.Height);
        builder.Append(BoxDrawing.BottomBorder(layout.Width));

        return FrameNormalizer.NormalizeToViewport(builder.ToString(), state.WindowWidth, state.WindowHeight);
    }

    private static string RenderBody(AppState state, Rect body)
    {
        return PlayScreenCatalog.Get(state.ActiveScreen).Render(state, body);
    }

    private static string RenderBodyWithModal(AppState state, Rect body, int drawableWidth)
    {
        var backgroundContent = RenderBody(state, body);
        var modalContent = ModalRenderer.Render(state.Modal!, drawableWidth);
        
        return OverlayModal(backgroundContent, modalContent, body.Width, body.Height);
    }

    private static string OverlayModal(string background, string modal, int width, int height)
    {
        var backgroundLines = background.Split(Environment.NewLine, StringSplitOptions.None);
        var modalLines = modal.Split(Environment.NewLine, StringSplitOptions.None);
        
        var modalHeight = modalLines.Length;
        var modalWidth = modalLines.Max(line => AnsiUtility.GetVisibleLength(line));
        
        var startRow = Math.Max(0, (height - modalHeight) / 2);
        var startCol = Math.Max(0, (width - modalWidth) / 2);
        
        var result = new List<string>(height);
        
        for (var row = 0; row < height; row++)
        {
            var backgroundLine = row < backgroundLines.Length ? backgroundLines[row] : string.Empty;
            
            var modalRow = row - startRow;
            if (modalRow >= 0 && modalRow < modalLines.Length)
            {
                var modalLine = modalLines[modalRow];
                var overlayedLine = OverlayLine(backgroundLine, modalLine, startCol, width);
                result.Add(overlayedLine);
            }
            else
            {
                result.Add(TextLayout.TruncateVisible(backgroundLine, width));
            }
        }
        
        return string.Join(Environment.NewLine, result);
    }

    private static string OverlayLine(string background, string overlay, int startCol, int width)
    {
        var bgLength = AnsiUtility.GetVisibleLength(background);
        var overlayLength = AnsiUtility.GetVisibleLength(overlay);
        
        if (startCol + overlayLength <= width)
        {
            var prefix = startCol > 0 && bgLength > 0 
                ? TextLayout.TruncateVisible(background, startCol) 
                : new string(' ', startCol);
            
            var suffixStart = startCol + overlayLength;
            var suffix = suffixStart < bgLength && suffixStart < width
                ? background.Substring(Math.Min(suffixStart, background.Length))
                : string.Empty;
            
            var combined = prefix + overlay + suffix;
            return TextLayout.TruncateVisible(combined, width);
        }
        
        return TextLayout.TruncateVisible(background, width);
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
