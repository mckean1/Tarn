using Tarn.ClientApp.Play.Rendering;

namespace Tarn.Client.Tests;

public sealed class LayoutTests
{
    [Fact]
    public void CreatesExpectedRects()
    {
        var layout = new Layout(100, 30);

        Assert.Equal(new Rect(0, 0, 100, 1), layout.Header);
        Assert.Equal(new Rect(0, 1, 100, 26), layout.Body);
        Assert.Equal(new Rect(0, 27, 100, 1), layout.MessageBar);
        Assert.Equal(new Rect(0, 28, 100, 2), layout.Footer);
        Assert.Equal(100, layout.Body.Right);
        Assert.Equal(27, layout.Body.Bottom);
    }

    [Fact]
    public void SplitsBodyIntoTwoColumns()
    {
        var layout = new Layout(100, 30);
        var (left, right) = layout.SplitBodyTwoColumns(40);

        Assert.Equal(new Rect(0, 1, 40, 26), left);
        Assert.Equal(new Rect(40, 1, 60, 26), right);
        Assert.Equal(left.Right, right.Left);
        Assert.True(layout.Body.Contains(left));
        Assert.True(layout.Body.Contains(right));
    }

    [Fact]
    public void RectSplitHelpersProduceNonOverlappingChildrenWithinParent()
    {
        var outer = new Rect(0, 0, 96, 18);
        var (top, bottom) = outer.SplitRows(5);
        var (topLeft, topRight) = top.SplitColumns(42, 2);
        var (bottomLeft, bottomRight) = bottom.SplitColumns(42, 2);

        Assert.Equal(5, top.Height);
        Assert.Equal(13, bottom.Height);
        Assert.Equal(top.Bottom, bottom.Top);
        Assert.Equal(topLeft.Right + 2, topRight.Left);
        Assert.Equal(bottomLeft.Right + 2, bottomRight.Left);
        Assert.True(outer.Contains(topLeft));
        Assert.True(outer.Contains(topRight));
        Assert.True(outer.Contains(bottomLeft));
        Assert.True(outer.Contains(bottomRight));
        Assert.Equal(96, topLeft.Width + 2 + topRight.Width);
        Assert.Equal(96, bottomLeft.Width + 2 + bottomRight.Width);
    }

    [Fact]
    public void InnerRectStaysInsideOuterRect()
    {
        var outer = new Rect(4, 2, 12, 6);
        var inner = outer.GetInnerRect();

        Assert.Equal(new Rect(5, 3, 10, 4), inner);
        Assert.True(outer.Contains(inner));
    }

    [Theory]
    [InlineData(120, LayoutMode.ThreeColumn)]
    [InlineData(90, LayoutMode.TwoColumn)]
    [InlineData(60, LayoutMode.SingleColumn)]
    public void ChoosesFallbackLayoutMode(int width, LayoutMode expected)
    {
        var layout = new Layout(width, 30);
        Assert.Equal(expected, layout.ChooseColumns());
    }
}
