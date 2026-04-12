using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class AppRendererTests
{
    private static AppState BuildStateWithModalAndScreen(int width, int height)
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
            WindowWidth = width,
            WindowHeight = height,
            ActiveScreen = ScreenId.Dashboard,
            MessageBar = new MessageBarState(MessageSeverity.Info, "Ready"),
            Modal = new ModalState
            {
                Kind = ModalKind.Confirmation,
                Title = "Test Modal",
                Lines = ["Modal content line"],
                PendingAction = new PendingAction(PendingActionKind.AdvanceWeek, "Advance", "Test"),
            },
        };

        var refreshService = new RefreshService();
        refreshService.RefreshAll(state);

        return state;
    }

    [Fact]
    public void ModalRendersInsideVisibleFrame()
    {
        var state = BuildStateWithModalAndScreen(100, 30);
        var renderer = new AppRenderer();

        var output = renderer.Render(state);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.None);

        // Modal should be rendered within the visible frame height
        Assert.InRange(lines.Length, 1, 30);

        // Modal content should appear somewhere in the output
        var plainOutput = AnsiUtility.StripAnsi(output);
        Assert.Contains("Test Modal", plainOutput);
        Assert.Contains("Modal content line", plainOutput);
        Assert.Contains("Enter/Y Confirm", plainOutput);
    }

    [Fact]
    public void ModalDoesNotAppendBeyondFrame()
    {
        var state = BuildStateWithModalAndScreen(100, 25);
        var renderer = new AppRenderer();

        var output = renderer.Render(state);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.None);

        // Output should be exactly the viewport height, not exceed it
        Assert.Equal(25, lines.Length);
    }

    [Fact]
    public void ModalRendersInCenterOfBodyArea()
    {
        var state = BuildStateWithModalAndScreen(100, 30);
        var renderer = new AppRenderer();

        var output = renderer.Render(state);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.None);

        // Find where the modal title appears
        var modalTitleLine = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var plain = AnsiUtility.StripAnsi(lines[i]);
            if (plain.Contains("Test Modal"))
            {
                modalTitleLine = i;
                break;
            }
        }

        // Modal should not be at the very top (header is there) or very bottom (footer is there)
        Assert.InRange(modalTitleLine, 3, lines.Length - 4);
    }

    [Fact]
    public void FrameWithoutModalRendersNormally()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
            WindowWidth = 100,
            WindowHeight = 30,
            ActiveScreen = ScreenId.Dashboard,
            MessageBar = new MessageBarState(MessageSeverity.Info, "Ready"),
            Modal = null,
        };

        var refreshService = new RefreshService();
        refreshService.RefreshAll(state);

        var renderer = new AppRenderer();
        var output = renderer.Render(state);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.None);

        Assert.Equal(30, lines.Length);

        // Should not contain modal-related content
        var plainOutput = AnsiUtility.StripAnsi(output);
        Assert.DoesNotContain("Test Modal", plainOutput);
    }

    [Fact]
    public void ModalOverlaysBackgroundContent()
    {
        var state = BuildStateWithModalAndScreen(100, 30);
        var renderer = new AppRenderer();

        var outputWithModal = renderer.Render(state);
        
        // Remove modal to compare
        state.Modal = null;
        var outputWithoutModal = renderer.Render(state);

        var plainWithModal = AnsiUtility.StripAnsi(outputWithModal);
        var plainWithoutModal = AnsiUtility.StripAnsi(outputWithoutModal);

        // Both should have the same number of lines (modal is overlaid, not appended)
        var linesWithModal = plainWithModal.Split(Environment.NewLine, StringSplitOptions.None);
        var linesWithoutModal = plainWithoutModal.Split(Environment.NewLine, StringSplitOptions.None);
        
        Assert.Equal(linesWithoutModal.Length, linesWithModal.Length);

        // Modal version should contain modal-specific text
        Assert.Contains("Test Modal", plainWithModal);
        Assert.DoesNotContain("Test Modal", plainWithoutModal);
    }

    [Fact]
    public void SmallTerminalStillRendersModal()
    {
        var state = BuildStateWithModalAndScreen(50, 15);
        var renderer = new AppRenderer();

        var output = renderer.Render(state);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.None);

        Assert.Equal(15, lines.Length);

        var plainOutput = AnsiUtility.StripAnsi(output);
        Assert.Contains("Test Modal", plainOutput);
    }
}
