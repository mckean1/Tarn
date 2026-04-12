using Tarn.ClientApp.Play.Rendering;

namespace Tarn.Client.Tests;

public sealed class TextLayoutTests
{
    [Fact]
    public void VisibleLengthIgnoresAnsiEscapeSequences()
    {
        var text = TerminalStyle.BrightWhite + "Hello" + TerminalStyle.Reset;

        Assert.Equal(5, AnsiUtility.GetVisibleLength(text));
        Assert.Equal("Hello", AnsiUtility.StripAnsi(text));
    }

    [Fact]
    public void TruncateVisibleUsesVisibleWidthForAnsiStyledText()
    {
        var text = TerminalStyle.BrightWhite + "Advance Week" + TerminalStyle.Reset;
        var truncated = TextLayout.TruncateVisible(text, 8);

        Assert.Equal(8, AnsiUtility.GetVisibleLength(truncated));
        Assert.Equal("Advance.", AnsiUtility.StripAnsi(truncated));
    }

    [Fact]
    public void PadVisibleRightUsesVisibleWidthForAnsiStyledText()
    {
        var text = TerminalStyle.BrightWhite + "Go" + TerminalStyle.Reset;
        var padded = TextLayout.PadVisibleRight(text, 5);

        Assert.Equal(5, AnsiUtility.GetVisibleLength(padded));
        Assert.Equal("Go   ", AnsiUtility.StripAnsi(padded));
    }
}
