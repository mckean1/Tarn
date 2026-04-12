using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;
using Tarn.Domain;

namespace Tarn.Client.Tests;

public sealed class ChromeRendererTests
{
    [Fact]
    public void HeaderIncludesTitleSeasonLeagueAndCashAsOneLine()
    {
        var state = BuildState();
        var output = HeaderRenderer.Render(state, 80);

        Assert.Contains("Dashboard", output);
        Assert.Contains("Year 1 Week 1", output);
        Assert.Contains("Bronze", output);
        Assert.Contains($"Cash {state.HumanPlayer.Cash}", output);
        Assert.DoesNotContain("You", output);
        Assert.DoesNotContain("1 Dash", output);
    }

    [Fact]
    public void FooterUsesSimplifiedDashboardControlsAndSecondaryJumps()
    {
        var output = FooterRenderer.Render(ScreenId.Dashboard, 80);
        Assert.Contains("↑↓ Move", output);
        Assert.Contains("Enter Select", output);
        Assert.Contains("Advance Week", output);
        Assert.Contains("Screens 1 Dash", output);
        Assert.DoesNotContain("Jump:", output);
    }

    [Fact]
    public void ScheduleFooterKeepsPrimaryControlsConcise()
    {
        var output = FooterRenderer.Render(ScreenId.Schedule, 80);

        Assert.Contains("↑↓ Move", output);
        Assert.Contains("←→ Week", output);
        Assert.Contains("Enter Replay", output);
        Assert.Contains("Esc Back", output);
        Assert.Contains("? Help", output);
    }

    [Fact]
    public void CollectionFooterKeepsBrowseControlsInFooterLayer()
    {
        var output = FooterRenderer.Render(ScreenId.Collection, 80);

        Assert.Contains("↑↓ Move", output);
        Assert.Contains("←→ Filter", output);
        Assert.Contains("Enter Sort", output);
        Assert.Contains("Esc Back", output);
        Assert.Contains("? Help", output);
    }

    [Fact]
    public void DeckFooterKeepsAutoBuildActionInFooterLayer()
    {
        var output = FooterRenderer.Render(ScreenId.Deck, 80);

        Assert.Contains("↑↓ Move", output);
        Assert.Contains("Enter Auto-build", output);
        Assert.Contains("Esc Back", output);
        Assert.Contains("? Help", output);
    }

    [Fact]
    public void CollectorFooterKeepsTabControlsConcise()
    {
        var output = FooterRenderer.Render(ScreenId.Collector, 80);

        Assert.Contains("↑↓ Move", output);
        Assert.Contains("←→ Tabs", output);
        Assert.Contains("Enter Confirm", output);
        Assert.Contains("Esc Back", output);
        Assert.Contains("? Help", output);
    }

    [Fact]
    public void MatchCenterFooterUsesBattleFocusedControls()
    {
        var output = FooterRenderer.Render(ScreenId.MatchCenter, 80);

        Assert.Contains("N Next", output);
        Assert.Contains("R Round", output);
        Assert.Contains("P Autoplay", output);
        Assert.Contains("Esc Back", output);
        Assert.Contains("? Help", output);
    }

    [Fact]
    public void HeaderUsesViewedScheduleWeekWhenScheduleIsActive()
    {
        var state = BuildState();
        state.ActiveScreen = ScreenId.Schedule;
        state.Schedule.SelectedWeek = 3;

        var output = HeaderRenderer.Render(state, 80);

        Assert.Contains("Schedule", output);
        Assert.Contains("Year 1 Week 3", output);
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

    [Fact]
    public void AppRendererWrapsDashboardInBoxedChrome()
    {
        var state = BuildState();
        state.Dashboard.SelectedActionIndex = 1;
        state.Dashboard.ViewModel = new DashboardQueries().Build(state.World, state.HumanPlayerId);

        var output = new AppRenderer().Render(state);
        var plainOutput = AnsiUtility.StripAnsi(output);
        var lines = plainOutput.Split(Environment.NewLine);

        Assert.Contains("┌ TARN ", plainOutput);
        Assert.Contains("Dashboard · Year 1 Week 1 · Bronze", plainOutput);
        Assert.Contains("Season Status", plainOutput);
        Assert.Contains("Recommended Actions", plainOutput);
        Assert.Contains("├", plainOutput);
        Assert.Contains("└", plainOutput);
        Assert.Equal(state.WindowHeight, lines.Length);
        Assert.All(lines.Take(state.WindowHeight), line => Assert.Equal(FrameNormalizer.GetDrawableWidth(state.WindowWidth), AnsiUtility.GetVisibleLength(line)));
        Assert.StartsWith("┌", lines[0]);
        Assert.StartsWith("├", lines[2]);
        Assert.StartsWith("├", lines[^4]);
        Assert.StartsWith("└", lines[^1]);
    }

    [Fact]
    public void AppRendererRecomputesFrameWhenWindowSizeChanges()
    {
        var state = BuildState();
        state.Dashboard.ViewModel = new DashboardQueries().Build(state.World, state.HumanPlayerId);
        var renderer = new AppRenderer();

        state.WindowWidth = 90;
        state.WindowHeight = 20;
        var narrow = AnsiUtility.StripAnsi(renderer.Render(state)).Split(Environment.NewLine);

        state.WindowWidth = 110;
        state.WindowHeight = 24;
        var wide = AnsiUtility.StripAnsi(renderer.Render(state)).Split(Environment.NewLine);

        Assert.Equal(20, narrow.Length);
        Assert.Equal(24, wide.Length);
        Assert.All(narrow, line => Assert.Equal(FrameNormalizer.GetDrawableWidth(90), AnsiUtility.GetVisibleLength(line)));
        Assert.All(wide, line => Assert.Equal(FrameNormalizer.GetDrawableWidth(110), AnsiUtility.GetVisibleLength(line)));
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
