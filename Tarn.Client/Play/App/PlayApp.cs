using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.Collection;
using Tarn.ClientApp.Play.Screens.Collector;
using Tarn.ClientApp.Play.Screens.Dashboard;
using Tarn.ClientApp.Play.Screens.Deck;
using Tarn.ClientApp.Play.Screens.League;
using Tarn.ClientApp.Play.Screens.Market;
using Tarn.ClientApp.Play.Screens.MatchCenter;
using Tarn.ClientApp.Play.Screens.Schedule;
using Tarn.ClientApp.Play.Screens.WeekSummary;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.App;

public sealed class PlayApp
{
    private readonly RefreshService refreshService = new();
    private readonly ActionExecutor actionExecutor;
    private readonly FrameRedrawTracker frameRedrawTracker = new();
    private readonly AppRenderer renderer = new();
    private readonly AppState state;

    public PlayApp(string storagePath, World world)
    {
        actionExecutor = new ActionExecutor(refreshService);
        state = refreshService.CreateInitialState(storagePath, world);
    }

    public void Run()
    {
        TrySetCursorVisible(false);
        try
        {
            while (!state.ShouldQuit)
            {
                UpdateWindowSize();
                Render();

                if (!ReplayAutoplay.WaitForInputOrTick(state))
                {
                    TickMessageBar();
                    ReplayAutoplay.AdvanceTick(state);
                    continue;
                }

                HandleInput(Console.ReadKey(intercept: true));
            }
        }
        finally
        {
            TrySetCursorVisible(true);
            TryClear();
        }
    }

    private void HandleInput(ConsoleKeyInfo key)
    {
        TickMessageBar();
        var action = KeyMap.Map(key);
        if (action == InputAction.None)
        {
            return;
        }

        if (state.Modal is not null)
        {
            HandleModal(action);
            return;
        }

        if (PlayScreenCatalog.TryGetShortcut(action, out var shortcutScreen))
        {
            NavigateToScreen(shortcutScreen.Id);
            return;
        }

        switch (action)
        {
            case InputAction.Quit:
                state.ShouldQuit = true;
                return;
            case InputAction.ToggleHelp:
                state.Modal = BuildHelpModal();
                frameRedrawTracker.Invalidate();
                return;
        }

        var screen = PlayScreenCatalog.Get(state.ActiveScreen);
        ApplyResult(screen.Controller.Handle(state, action));
    }

    private void HandleModal(InputAction action)
    {
        if (action == InputAction.Back || action == InputAction.Select)
        {
            state.Modal = null;
            state.MessageBar = new MessageBarState(MessageSeverity.Info, "Closed.");
            frameRedrawTracker.Invalidate();
            return;
        }

        if (action == InputAction.Confirm)
        {
            actionExecutor.ExecutePending(state);
        }
    }

    private void ApplyResult(ScreenControllerResult result)
    {
        var requiresFullRedraw = false;

        if (result.Modal is not null)
        {
            state.Modal = result.Modal;
            requiresFullRedraw = true;
        }

        if (result.Message is not null)
        {
            state.MessageBar = result.Message;
        }

        if (result.NavigateTo is { } screen)
        {
            NavigateToScreen(screen);
            requiresFullRedraw = true;
        }

        if (result.RequiresRefresh)
        {
            refreshService.RefreshAll(state);
            requiresFullRedraw = true;
        }

        if (requiresFullRedraw)
        {
            frameRedrawTracker.Invalidate();
        }
    }

    private void Render()
    {
        if (frameRedrawTracker.BeginRender(state))
        {
            TryClear();
        }

        WriteFrame(renderer.Render(state));
    }

    private void UpdateWindowSize()
    {
        frameRedrawTracker.UpdateWindow(state, Console.WindowWidth, Console.WindowHeight);
    }

    private ModalState BuildHelpModal()
    {
        var screen = PlayScreenCatalog.Get(state.ActiveScreen);

        return new ModalState
        {
            Kind = ModalKind.Help,
            Title = "Tarn Help",
            Lines =
            [
                PlayScreenCatalog.BuildGlobalNavigationText(compact: false, prefix: "Global nav: "),
                "Global controls: arrows move, Enter select, Esc back, Q quit, ? or H help",
                screen.HelpText,
                "Confirmation: Y confirms and Esc cancels.",
            ],
        };
    }

    private void NavigateToScreen(ScreenId screenId)
    {
        if (screenId == ScreenId.MatchCenter)
        {
            state.MatchCenter.ReturnScreen = state.ActiveScreen;
        }

        NavigationService.Navigate(state, screenId);
        var screen = PlayScreenCatalog.Get(screenId);
        screen.Refresh?.Invoke(refreshService, state);
        frameRedrawTracker.Invalidate();
    }

    private void TickMessageBar()
    {
        if (state.MessageBar is null)
        {
            return;
        }

        var remaining = state.MessageBar.TurnsToLive - 1;
        state.MessageBar = remaining <= 0 ? null : state.MessageBar with { TurnsToLive = remaining };
    }

    private static void TrySetCursorVisible(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch (IOException)
        {
        }
    }

    private static void TrySetCursorPosition(int left, int top)
    {
        try
        {
            Console.SetCursorPosition(left, top);
        }
        catch (IOException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    private static void TryClear()
    {
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
        }
    }

    private void WriteFrame(string frame)
    {
        var lines = FrameNormalizer.NormalizeLines(frame, state.WindowWidth, state.WindowHeight);
        for (var row = 0; row < lines.Count; row++)
        {
            TrySetCursorPosition(0, row);
            Console.Write(lines[row]);
        }

        TrySetCursorPosition(0, 0);
    }
}
