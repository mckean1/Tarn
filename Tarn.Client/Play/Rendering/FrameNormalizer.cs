namespace Tarn.ClientApp.Play.Rendering;

public static class FrameNormalizer
{
    public static string Normalize(string frame, int width, int height) =>
        string.Join(Environment.NewLine, NormalizeLines(frame, width, height));

    public static IReadOnlyList<string> NormalizeLines(string? frame, int width, int height)
    {
        var safeWidth = Math.Max(0, width);
        var safeHeight = Math.Max(0, height);
        if (safeWidth == 0 || safeHeight == 0)
        {
            return [];
        }

        var sourceLines = (frame ?? string.Empty).Split(Environment.NewLine, StringSplitOptions.None);
        var normalizedLines = new List<string>(safeHeight);

        for (var index = 0; index < safeHeight; index++)
        {
            var line = index < sourceLines.Length ? sourceLines[index] : string.Empty;
            normalizedLines.Add(TextLayout.TruncateVisible(line, safeWidth));
        }

        return normalizedLines;
    }
}
