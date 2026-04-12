using Tarn.ClientApp.Play.Rendering;

namespace Tarn.Client.Tests;

public sealed class LayoutTests
{
    [Fact]
    public void CreatesExpectedRects()
    {
        var layout = new Layout(100, 30);

        Assert.Equal(new Rect(0, 0, 100, 3), layout.Header);
        Assert.Equal(new Rect(0, 3, 100, 24), layout.Body);
        Assert.Equal(new Rect(0, 27, 100, 1), layout.MessageBar);
        Assert.Equal(new Rect(0, 28, 100, 2), layout.Footer);
    }

    [Fact]
    public void SplitsBodyIntoTwoColumns()
    {
        var layout = new Layout(100, 30);
        var (left, right) = layout.SplitBodyTwoColumns(40);

        Assert.Equal(new Rect(0, 3, 40, 24), left);
        Assert.Equal(new Rect(40, 3, 60, 24), right);
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
