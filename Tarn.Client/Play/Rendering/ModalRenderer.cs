using System.Text;
using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public static class ModalRenderer
{
    public static string Render(ModalState modal, int width)
    {
        var innerWidth = Math.Max(20, Math.Min(width - 4, 72));
        var builder = new StringBuilder();
        builder.AppendLine("+" + new string('-', innerWidth) + "+");
        builder.AppendLine("|" + Layout.Truncate(modal.Title, innerWidth) + "|");
        builder.AppendLine("+" + new string('-', innerWidth) + "+");
        foreach (var line in modal.Lines)
        {
            builder.AppendLine("|" + Layout.Truncate(line, innerWidth) + "|");
        }

        var footer = modal.Kind switch
        {
            ModalKind.Help => "Enter/Esc closes this overlay.",
            ModalKind.Confirmation => "Y confirms. Esc cancels.",
            ModalKind.PackReveal => "Enter or Esc returns to the shop.",
            _ => string.Empty,
        };

        if (!string.IsNullOrEmpty(footer))
        {
            builder.AppendLine("|" + Layout.Truncate(footer, innerWidth) + "|");
        }

        builder.Append("+" + new string('-', innerWidth) + "+");
        return builder.ToString();
    }
}
