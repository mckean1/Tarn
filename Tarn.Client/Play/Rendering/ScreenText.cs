namespace Tarn.ClientApp.Play.Rendering;

public static class ScreenText
{
    public static string Divider(int width) => new string('-', Math.Max(8, width));

    public static string FitBlock(string text, int width, int height) => FitLines(text.Split(Environment.NewLine), width, height);

    public static string FitLines(IEnumerable<string> lines, int width, int height)
    {
        return string.Join(Environment.NewLine, lines.Take(height).Select(line => Layout.Truncate(line, width)));
    }

    public static string EmptyState(string title, string body, int width)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"[{title}]",
                body,
            }.Select(line => Layout.Truncate(line, width)));
    }

    public static string StatusChip(string label) => $"[{label}]";

    public static string InteractiveRow(bool isSelected, string content, string selectedMarker = ">", string unselectedMarker = " ")
    {
        var row = $"{(isSelected ? selectedMarker : unselectedMarker)} {content}";
        return isSelected ? TerminalStyle.Selected(row) : row;
    }

    public static string Secondary(string text) => TerminalStyle.Secondary(text);

    public static IReadOnlyList<string> WrapLines(string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [string.Empty];
        }

        var safeWidth = Math.Max(1, width);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (Layout.VisibleLength(candidate) <= safeWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(Layout.Truncate(current, safeWidth));
                current = word;
                continue;
            }

            lines.Add(Layout.Truncate(word, safeWidth));
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(Layout.Truncate(current, safeWidth));
        }

        return lines;
    }
}
