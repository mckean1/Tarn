using Tarn.ClientApp.Play.Rendering;

namespace Tarn.Client.Tests;

public sealed class FrameNormalizerTests
{
    [Fact]
    public void NormalizePadsEachLineToTargetWidth()
    {
        var lines = FrameNormalizer.NormalizeLines("A" + Environment.NewLine + "BBB", 5, 2);

        Assert.Equal(2, lines.Count);
        Assert.Equal("A    ", lines[0]);
        Assert.Equal("BBB  ", lines[1]);
    }

    [Fact]
    public void NormalizePadsFramesToTargetHeight()
    {
        var lines = FrameNormalizer.NormalizeLines("Alpha", 5, 3);

        Assert.Equal(3, lines.Count);
        Assert.Equal("Alpha", lines[0]);
        Assert.Equal("     ", lines[1]);
        Assert.Equal("     ", lines[2]);
    }

    [Fact]
    public void NormalizeTruncatesLongLinesToTargetWidth()
    {
        var lines = FrameNormalizer.NormalizeLines("Alphabet", 5, 1);

        Assert.Single(lines);
        Assert.Equal("Alph.", lines[0]);
    }

    [Fact]
    public void NormalizeProducesRectangularFrame()
    {
        var frame = FrameNormalizer.Normalize("A" + Environment.NewLine + "Longer line", 6, 4);
        var lines = frame.Split(Environment.NewLine, StringSplitOptions.None);

        Assert.Equal(4, lines.Length);
        Assert.All(lines, line => Assert.Equal(6, AnsiUtility.GetVisibleLength(line)));
        Assert.Equal("A     ", lines[0]);
        Assert.Equal("Longe.", lines[1]);
        Assert.Equal("      ", lines[2]);
        Assert.Equal("      ", lines[3]);
    }
}
