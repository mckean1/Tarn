namespace Tarn.ClientApp.Play.Rendering;

public static class BoxDrawing
{
    public static string TopBorder(int innerWidth, string title = "")
    {
        var safeInnerWidth = Math.Max(4, innerWidth);
        var safeTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : $" {title.Trim()} ";
        if (AnsiUtility.GetVisibleLength(safeTitle) > safeInnerWidth)
        {
            var trimmedTitle = TextLayout.TruncateVisible(title.Trim(), Math.Max(1, safeInnerWidth - 2)).TrimEnd();
            safeTitle = $" {trimmedTitle} ";
            if (AnsiUtility.GetVisibleLength(safeTitle) > safeInnerWidth)
            {
                safeTitle = TextLayout.TruncateVisible(safeTitle, safeInnerWidth);
            }
        }

        return "┌" + safeTitle + new string('─', safeInnerWidth - AnsiUtility.GetVisibleLength(safeTitle)) + "┐";
    }

    public static string Divider(int innerWidth) => "├" + new string('─', Math.Max(4, innerWidth)) + "┤";

    public static string BottomBorder(int innerWidth) => "└" + new string('─', Math.Max(4, innerWidth)) + "┘";

    public static string FrameLine(string content, int innerWidth) => $"│{TextLayout.TruncateVisible(content ?? string.Empty, Math.Max(4, innerWidth))}│";

    public static IReadOnlyList<string> RenderBox(string title, IReadOnlyList<string> content, int width, int height)
    {
        var safeWidth = Math.Max(12, width);
        var safeHeight = Math.Max(3, height);
        var innerWidth = safeWidth - 2;
        var lines = new List<string>(safeHeight)
        {
            TopBorder(innerWidth, title),
        };

        var contentHeight = safeHeight - 2;
        for (var index = 0; index < contentHeight; index++)
        {
            lines.Add(FrameLine(index < content.Count ? content[index] : string.Empty, innerWidth));
        }

        lines.Add(BottomBorder(innerWidth));
        return lines;
    }

    public static IReadOnlyList<string> MergeColumns(IReadOnlyList<string> left, IReadOnlyList<string> right, int gap = 1)
    {
        var leftWidth = left.Count == 0 ? 0 : AnsiUtility.GetVisibleLength(left[0]);
        var rightWidth = right.Count == 0 ? 0 : AnsiUtility.GetVisibleLength(right[0]);
        var rowCount = Math.Max(left.Count, right.Count);
        var gapText = new string(' ', Math.Max(0, gap));
        var lines = new List<string>(rowCount);

        for (var index = 0; index < rowCount; index++)
        {
            var leftLine = index < left.Count ? TextLayout.PadVisibleRight(left[index], leftWidth) : new string(' ', leftWidth);
            var rightLine = index < right.Count ? TextLayout.PadVisibleRight(right[index], rightWidth) : new string(' ', rightWidth);
            lines.Add(leftLine + gapText + rightLine);
        }

        return lines;
    }

    public static string ComposeColumns(int width, string left, string center, string right)
    {
        var safeWidth = Math.Max(12, width);
        var combined = $"{left}  {center}  {right}";
        if (combined.Length > safeWidth || left.Length + center.Length + right.Length + 4 > safeWidth)
        {
            return Layout.Truncate(combined, safeWidth);
        }

        var buffer = Enumerable.Repeat(' ', safeWidth).ToArray();
        Write(buffer, 0, left);
        Write(buffer, (safeWidth - center.Length) / 2, center);
        Write(buffer, safeWidth - right.Length, right);
        return new string(buffer);
    }

    private static void Write(char[] buffer, int startIndex, string value)
    {
        for (var index = 0; index < value.Length && startIndex + index < buffer.Length; index++)
        {
            buffer[startIndex + index] = value[index];
        }
    }
}
