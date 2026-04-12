using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Screens.Dashboard;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class WeekSummaryTests
{
    [Theory]
    [InlineData(3, "+3")]
    [InlineData(0, "0")]
    [InlineData(-2, "-2")]
    public void FormatsSignedDelta(int value, string expected)
    {
        Assert.Equal(expected, WeekSummaryQueries.FormatSignedDelta(value));
    }

    [Fact]
    public void DashboardAdvanceWeekProducesConfirmationModal()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var state = new RefreshService().CreateInitialState("test.json", world);
        var controller = new DashboardController();

        var result = controller.Handle(state, InputAction.AdvanceWeek);

        Assert.NotNull(result.Modal);
        Assert.Equal("Advance Week?", result.Modal!.Title);
        Assert.Equal(PendingActionKind.AdvanceWeek, result.Modal.PendingAction!.Kind);
    }
}
