using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Screens.Schedule;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class ScheduleTests
{
    [Fact]
    public void ScheduleQueryClampsSelection()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var model = new ScheduleQueries().Build(world, 1, 999, world.Players.Values.Single(player => player.IsHuman).Id);

        Assert.True(model.SelectedFixtureIndex < model.Fixtures.Count);
    }

    [Fact]
    public void ScheduleControllerChangesWeekAndRefreshes()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new RefreshService().CreateInitialState("test.json", world);
        var controller = new ScheduleController();

        state.Schedule.SelectedWeek = 1;
        var result = controller.Handle(state, InputAction.MoveLeft);

        Assert.True(result.RequiresRefresh);
        Assert.Equal(1, state.Schedule.SelectedWeek);
    }
}
