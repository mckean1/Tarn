using System.Text;

namespace Tarn.ClientApp.Play.Rendering;

public static class TextLayout
{
    public static string PadVisibleRight(string? text, int width)
    {
        var safeWidth = Math.Max(0, width);
        var value = text ?? string.Empty;
        var visibleLength = AnsiUtility.GetVisibleLength(value);
        return visibleLength >= safeWidth
            ? value
            : value + new string(' ', safeWidth - visibleLength);
    }

    public static string TruncateVisible(string? text, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var value = text ?? string.Empty;
        var visibleLength = AnsiUtility.GetVisibleLength(value);
        if (visibleLength <= width)
        {
            return PadVisibleRight(value, width);
        }

        if (width == 1)
        {
            return ".";
        }

        var truncated = TakeVisible(value, width - 1);
        return TerminalStyle.ContainsAnsi(value)
            ? truncated + "." + TerminalStyle.Reset
            : truncated + ".";
    }

    private static string TakeVisible(string value, int visibleLength)
    {
        if (visibleLength <= 0 || string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var written = 0;
        for (var index = 0; index < value.Length && written < visibleLength; index++)
        {
            if (AnsiUtility.IsAnsiSequenceStart(value, index))
            {
                var endIndex = AnsiUtility.SkipAnsiSequence(value, index);
                builder.Append(value, index, endIndex - index);
                index = endIndex - 1;
                continue;
            }

            builder.Append(value[index]);
            written++;
        }

        return builder.ToString();
    }
}
