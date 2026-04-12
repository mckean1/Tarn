using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.League;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class LeagueTests
{
    [Fact]
    public void LeagueRendererMarksHumanRow()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState
        {
            World = world,
            HumanPlayerId = human.Id,
            StoragePath = "test.json",
        };
        state.League.ViewModel = new LeagueQueries().Build(world, human.Id, 0, 0);

        var output = LeagueRenderer.Render(state, new Rect(0, 0, 100, 20));

        Assert.Contains("You", output);
        Assert.DoesNotContain("(You)", output);
        Assert.Contains("Form", output);
    }

    [Fact]
    public void LeagueQueryUsesFantasyAiNamesInsteadOfPlayerNumbers()
    {
        var world = new WorldFactory().CreateNewWorld(1, "You");
        var human = world.Players.Values.Single(player => player.IsHuman);

        var model = new LeagueQueries().Build(world, human.Id, 0, 0);

        Assert.DoesNotContain(model.Rows.Select(row => row.PlayerName), name => name.StartsWith("Player ", StringComparison.Ordinal));
    }

    [Fact]
    public void TableRendererKeepsLeagueColumnsAligned()
    {
        var output = TableRenderer.Render(
        [
            new TableColumn { Header = "", Width = 1 },
            new TableColumn { Header = "Rk", Width = 3 },
            new TableColumn { Header = "Player", Width = 12 },
            new TableColumn { Header = "W-L", Width = 5 },
            new TableColumn { Header = "Pts", Width = 4 },
            new TableColumn { Header = "GD", Width = 4 },
            new TableColumn { Header = "Form", Width = 5 },
        ],
        [
            new[] { "*", "1", "You", "2-0", "6", "3", "WW" },
        ]);

        Assert.Contains("Rk ", output);
        Assert.Contains("*|1", output);
    }
}
