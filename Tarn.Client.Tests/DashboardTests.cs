using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Screens.Dashboard;
using Tarn.ClientApp.Play.Rendering;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class DashboardTests
{
    [Fact]
    public void DashboardQueryBuildsRealSummary()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var model = new DashboardQueries().Build(world, human.Id);

        Assert.Equal(1, model.Year);
        Assert.Equal("Bronze", model.League);
        Assert.Contains("LEGAL", model.DeckLegality);
        Assert.NotEmpty(model.RecommendedActions);
    }

    [Fact]
    public void DashboardRendererHandlesPartialData()
    {
        var state = BuildState();
        state.Dashboard.ViewModel = new DashboardViewModel(1, 1, "Bronze", "0-0", "Rank 1", 100, "LEGAL", "31/31 cards", "No fixture scheduled this week.", ["No completed matches yet."], ["Open Schedule"]);

        var output = DashboardRenderer.Render(state, new Rect(0, 0, 80, 12));

        Assert.Contains("Dashboard", output);
        Assert.Contains("No completed matches yet.", output);
        Assert.Contains("> Open Schedule", output);
    }

    private static AppState BuildState()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        return new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
    }
}
