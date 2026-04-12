namespace Tarn.ClientApp.Play.Rendering;

public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(Rect other) =>
        other.Left >= Left
        && other.Top >= Top
        && other.Right <= Right
        && other.Bottom <= Bottom;

    public Rect Inset(int horizontalPadding, int verticalPadding)
    {
        var safeHorizontalPadding = Math.Max(0, horizontalPadding);
        var safeVerticalPadding = Math.Max(0, verticalPadding);
        var horizontalTrim = Math.Min(Width, safeHorizontalPadding * 2);
        var verticalTrim = Math.Min(Height, safeVerticalPadding * 2);
        return new Rect(
            X + Math.Min(safeHorizontalPadding, Width),
            Y + Math.Min(safeVerticalPadding, Height),
            Width - horizontalTrim,
            Height - verticalTrim);
    }

    public Rect GetInnerRect() => Inset(1, 1);

    public (Rect Left, Rect Right) SplitColumns(int leftWidth, int gap = 0)
    {
        var safeGap = Math.Clamp(gap, 0, Math.Max(0, Width));
        var safeLeftWidth = Math.Clamp(leftWidth, 0, Math.Max(0, Width - safeGap));
        var rightX = X + safeLeftWidth + safeGap;
        return (
            new Rect(X, Y, safeLeftWidth, Height),
            new Rect(rightX, Y, Right - rightX, Height));
    }

    public (Rect Top, Rect Bottom) SplitRows(int topHeight, int gap = 0)
    {
        var safeGap = Math.Clamp(gap, 0, Math.Max(0, Height));
        var safeTopHeight = Math.Clamp(topHeight, 0, Math.Max(0, Height - safeGap));
        var bottomY = Y + safeTopHeight + safeGap;
        return (
            new Rect(X, Y, Width, safeTopHeight),
            new Rect(X, bottomY, Width, Bottom - bottomY));
    }
}

public sealed class Layout
{
    public const int HeaderHeight = 1;
    public const int MessageBarHeight = 1;
    public const int FooterHeight = 2;

    public Layout(int width, int height)
    {
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
    }

    public int Width { get; }
    public int Height { get; }
    public bool IsNarrow => Width < 100;
    public bool IsVeryNarrow => Width < 72;
    public Rect Header => new(0, 0, Width, HeaderHeight);
    public Rect Body => new(0, HeaderHeight, Width, Math.Max(0, Height - HeaderHeight - MessageBarHeight - FooterHeight));
    public Rect MessageBar => new(0, Body.Bottom, Width, MessageBarHeight);
    public Rect Footer => new(0, MessageBar.Bottom, Width, FooterHeight);

    public (Rect Left, Rect Right) SplitBodyTwoColumns(int leftWidth)
    {
        var minimumColumnWidth = Math.Min(10, Math.Max(0, Body.Width / 2));
        var maximumLeftWidth = Math.Max(minimumColumnWidth, Body.Width - minimumColumnWidth);
        var safeLeftWidth = Math.Clamp(leftWidth, minimumColumnWidth, maximumLeftWidth);
        return Body.SplitColumns(safeLeftWidth);
    }

    public (Rect Left, Rect Center, Rect Right) SplitBodyThreeColumns(int leftWidth, int centerWidth)
    {
        var minimumColumnWidth = Math.Min(8, Math.Max(0, Body.Width / 3));
        var maximumLeftWidth = Math.Max(minimumColumnWidth, Body.Width - (minimumColumnWidth * 2));
        var safeLeftWidth = Math.Clamp(leftWidth, minimumColumnWidth, maximumLeftWidth);
        var (left, remaining) = Body.SplitColumns(safeLeftWidth);
        var maximumCenterWidth = Math.Max(minimumColumnWidth, remaining.Width - minimumColumnWidth);
        var safeCenterWidth = Math.Clamp(centerWidth, minimumColumnWidth, maximumCenterWidth);
        var (center, right) = remaining.SplitColumns(safeCenterWidth);
        return (left, center, right);
    }

    public LayoutMode ChooseColumns() =>
        IsVeryNarrow ? LayoutMode.SingleColumn : IsNarrow ? LayoutMode.TwoColumn : LayoutMode.ThreeColumn;

    public static string Truncate(string value, int width)
        => TextLayout.TruncateVisible(value, width);

    public static int VisibleLength(string value) => AnsiUtility.GetVisibleLength(value);
}

public enum LayoutMode
{
    SingleColumn,
    TwoColumn,
    ThreeColumn,
}
