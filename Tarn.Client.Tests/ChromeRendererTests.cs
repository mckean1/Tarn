using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class ChromeRendererTests
{
    [Fact]
    public void HeaderIncludesWeekPlayerLeagueAndCash()
    {
        var state = BuildState();
        var output = HeaderRenderer.Render(state, 80);

        Assert.Contains("Tarn Play", output);
        Assert.Contains("Year 1, Week 1", output);
        Assert.Contains("You", output);
        Assert.Contains("Bronze", output);
        Assert.Contains($"Cash {state.HumanPlayer.Cash}", output);
    }

    [Fact]
    public void FooterUsesScreenSpecificControls()
    {
        var output = FooterRenderer.Render(ScreenId.MatchCenter, 80);
        Assert.Contains("N event", output);
        Assert.Contains("P autoplay", output);
    }

    [Fact]
    public void MessageBarPrefixesSeverity()
    {
        var output = MessageBarRenderer.Render(new MessageBarState(MessageSeverity.Success, "Saved."), 40);
        Assert.StartsWith("[SUCCESS]", output);
    }

    [Fact]
    public void HelpOverlayIncludesGlobalAndScreenSpecificControls()
    {
        var modal = new ModalState
        {
            Kind = ModalKind.Help,
            Title = "Tarn Help",
            Lines =
            [
                "Global nav",
                "Replay controls",
                "Confirmations",
            ],
        };

        var output = ModalRenderer.Render(modal, 60);
        Assert.Contains("Replay controls", output);
        Assert.Contains("Enter/Esc closes this overlay.", output);
    }

    [Fact]
    public void EmptyStateRendererProducesIntentionalCopy()
    {
        var output = ScreenText.EmptyState("No Replay", "Finish a fixture, then open it from Schedule.", 60);
        Assert.Contains("[No Replay]", output);
        Assert.Contains("Finish a fixture", output);
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
