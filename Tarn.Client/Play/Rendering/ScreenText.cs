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
}
