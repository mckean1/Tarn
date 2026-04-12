using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.Dashboard;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class ModalInputHandlingTests
{
    private static AppState BuildStateWithModal(ModalKind kind, PendingAction? pendingAction = null)
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
            Modal = new ModalState
            {
                Kind = kind,
                Title = "Test Modal",
                Lines = ["Test content"],
                PendingAction = pendingAction,
            },
        };
        return state;
    }

    [Fact]
    public void KeyMapMapsEnterToSelect()
    {
        var info = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        Assert.Equal(InputAction.Select, KeyMap.Map(info));
    }

    [Fact]
    public void KeyMapMapsYToConfirm()
    {
        var info = new ConsoleKeyInfo('y', ConsoleKey.Y, false, false, false);
        Assert.Equal(InputAction.Confirm, KeyMap.Map(info));
    }

    [Fact]
    public void KeyMapMapsEscToBack()
    {
        var info = new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);
        Assert.Equal(InputAction.Back, KeyMap.Map(info));
    }

    [Fact]
    public void DashboardBuildsAdvanceWeekConfirmationModal()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState 
        { 
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
        var controller = new DashboardController();

        var result = controller.Handle(state, InputAction.AdvanceWeek);

        Assert.NotNull(result.Modal);
        Assert.Equal(ModalKind.Confirmation, result.Modal!.Kind);
        Assert.Contains("Advance Week", result.Modal.Title);
        Assert.NotNull(result.Modal.PendingAction);
        Assert.Equal(PendingActionKind.AdvanceWeek, result.Modal.PendingAction!.Kind);
    }

    [Fact]
    public void SelectingAdvanceWeekActionBuildsModal()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState 
        { 
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
        state.Dashboard.SelectedActionIndex = 1; // Advance Week action
        var controller = new DashboardController();

        var result = controller.Handle(state, InputAction.Select);

        Assert.NotNull(result.Modal);
        Assert.Equal(ModalKind.Confirmation, result.Modal!.Kind);
        Assert.NotNull(result.Modal.PendingAction);
    }

    [Fact]
    public void AdvanceWeekModalDoesNotContainRedundantInstructions()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState 
        { 
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
        var controller = new DashboardController();

        var result = controller.Handle(state, InputAction.AdvanceWeek);

        Assert.NotNull(result.Modal);
        // Modal instructions are in the footer, not in modal lines
        Assert.DoesNotContain(result.Modal!.Lines, line => line.Contains("Press Y") || line.Contains("Esc"));
        Assert.Contains(result.Modal.Lines, line => line.Contains("Advance the world simulation"));
    }
}
