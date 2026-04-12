using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.Client.Tests;

public sealed class ModalRendererTests
{
    [Fact]
    public void ConfirmationModalShowsEnterAndYAsConfirmOptions()
    {
        var modal = new ModalState
        {
            Kind = ModalKind.Confirmation,
            Title = "Test Confirmation",
            Lines = ["Are you sure?"],
        };

        var output = ModalRenderer.Render(modal, 80);
        var plain = AnsiUtility.StripAnsi(output);

        Assert.Contains("Test Confirmation", plain);
        Assert.Contains("Are you sure?", plain);
        Assert.Contains("Enter/Y Confirm", plain);
        Assert.Contains("Esc Cancel", plain);
    }

    [Fact]
    public void HelpModalShowsEnterEscInstruction()
    {
        var modal = new ModalState
        {
            Kind = ModalKind.Help,
            Title = "Help",
            Lines = ["Help content here"],
        };

        var output = ModalRenderer.Render(modal, 80);
        var plain = AnsiUtility.StripAnsi(output);

        Assert.Contains("Help", plain);
        Assert.Contains("Enter/Esc closes this overlay", plain);
    }

    [Fact]
    public void PackRevealModalShowsEnterOrEscInstruction()
    {
        var modal = new ModalState
        {
            Kind = ModalKind.PackReveal,
            Title = "Pack Opened",
            Lines = ["Card 1", "Card 2"],
        };

        var output = ModalRenderer.Render(modal, 80);
        var plain = AnsiUtility.StripAnsi(output);

        Assert.Contains("Pack Opened", plain);
        Assert.Contains("Enter or Esc returns to the shop", plain);
    }

    [Fact]
    public void ModalRendersWithBorderAndFormattedLines()
    {
        var modal = new ModalState
        {
            Kind = ModalKind.Confirmation,
            Title = "Title",
            Lines = ["Line 1", "Line 2"],
        };

        var output = ModalRenderer.Render(modal, 50);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.None);

        Assert.StartsWith("+", lines[0]);
        Assert.EndsWith("+", lines[0]);
        Assert.StartsWith("|", lines[1]);
        Assert.Contains("Title", lines[1]);
        Assert.StartsWith("+", lines[2]);
        Assert.StartsWith("|", lines[3]);
        Assert.Contains("Line 1", lines[3]);
        Assert.StartsWith("|", lines[4]);
        Assert.Contains("Line 2", lines[4]);
    }

    [Fact]
    public void ModalRespectsMinimumAndMaximumWidth()
    {
        var modal = new ModalState
        {
            Kind = ModalKind.Confirmation,
            Title = "Title",
            Lines = ["Content"],
        };

        var narrowOutput = ModalRenderer.Render(modal, 25);
        var narrowLines = narrowOutput.Split(Environment.NewLine, StringSplitOptions.None);
        var narrowWidth = narrowLines[0].Length;

        Assert.InRange(narrowWidth, 20, 25);

        var wideOutput = ModalRenderer.Render(modal, 120);
        var wideLines = wideOutput.Split(Environment.NewLine, StringSplitOptions.None);
        var wideWidth = wideLines[0].Length;

        Assert.InRange(wideWidth, 70, 74);
    }
}
