namespace Tarn.ClientApp.Play.Rendering;

public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width - 1;
    public int Bottom => Y + Height - 1;
}

public sealed class Layout
{
    public const int HeaderHeight = 3;
    public const int MessageBarHeight = 1;
    public const int FooterHeight = 2;

    public Layout(int width, int height)
    {
        Width = Math.Max(40, width);
        Height = Math.Max(12, height);
    }

    public int Width { get; }
    public int Height { get; }
    public bool IsNarrow => Width < 100;
    public bool IsVeryNarrow => Width < 72;
    public Rect Header => new(0, 0, Width, HeaderHeight);
    public Rect Body => new(0, HeaderHeight, Width, Math.Max(3, Height - HeaderHeight - MessageBarHeight - FooterHeight));
    public Rect MessageBar => new(0, Body.Bottom + 1, Width, MessageBarHeight);
    public Rect Footer => new(0, MessageBar.Bottom + 1, Width, FooterHeight);

    public (Rect Left, Rect Right) SplitBodyTwoColumns(int leftWidth)
    {
        var safeLeftWidth = Math.Clamp(leftWidth, 10, Body.Width - 10);
        return (new Rect(Body.X, Body.Y, safeLeftWidth, Body.Height), new Rect(Body.X + safeLeftWidth, Body.Y, Body.Width - safeLeftWidth, Body.Height));
    }

    public (Rect Left, Rect Center, Rect Right) SplitBodyThreeColumns(int leftWidth, int centerWidth)
    {
        var safeLeft = Math.Clamp(leftWidth, 8, Body.Width - 16);
        var safeCenter = Math.Clamp(centerWidth, 8, Body.Width - safeLeft - 8);
        var rightWidth = Math.Max(8, Body.Width - safeLeft - safeCenter);
        return (
            new Rect(Body.X, Body.Y, safeLeft, Body.Height),
            new Rect(Body.X + safeLeft, Body.Y, safeCenter, Body.Height),
            new Rect(Body.X + safeLeft + safeCenter, Body.Y, rightWidth, Body.Height));
    }

    public LayoutMode ChooseColumns() =>
        IsVeryNarrow ? LayoutMode.SingleColumn : IsNarrow ? LayoutMode.TwoColumn : LayoutMode.ThreeColumn;

    public static string Truncate(string value, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(value) || value.Length <= width)
        {
            return value.PadRight(width);
        }

        if (width == 1)
        {
            return ".";
        }

        return value[..(width - 1)] + ".";
    }
}

public enum LayoutMode
{
    SingleColumn,
    TwoColumn,
    ThreeColumn,
}
