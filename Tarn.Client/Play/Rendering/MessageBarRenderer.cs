using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Rendering;

public static class MessageBarRenderer
{
    public static string Render(MessageBarState? message, int width)
    {
        if (message is null)
        {
            return new string(' ', width);
        }

        var prefix = message.Severity switch
        {
            MessageSeverity.Success => "[SUCCESS] ",
            MessageSeverity.Warning => "[WARN] ",
            MessageSeverity.Error => "[ERROR] ",
            _ => "[INFO] ",
        };
        return Layout.Truncate(prefix + message.Text, width);
    }
}
