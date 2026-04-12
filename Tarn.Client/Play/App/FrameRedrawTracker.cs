using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.App;

public sealed class FrameRedrawTracker
{
    private int lastWindowWidth = -1;
    private int lastWindowHeight = -1;
    private ScreenId? lastScreen;
    private bool invalidated = true;

    public bool UpdateWindow(AppState state, int consoleWidth, int consoleHeight)
    {
        var nextWidth = Math.Max(0, consoleWidth);
        var nextHeight = Math.Max(0, consoleHeight);
        var changed = state.WindowWidth != nextWidth || state.WindowHeight != nextHeight;

        state.WindowWidth = nextWidth;
        state.WindowHeight = nextHeight;
        state.IsNarrowLayout = FrameNormalizer.GetDrawableWidth(state.WindowWidth) < 100;

        if (changed)
        {
            Invalidate();
        }

        return changed;
    }

    public void Invalidate() => invalidated = true;

    public bool BeginRender(AppState state)
    {
        var requiresFullRedraw = invalidated
            || lastScreen != state.ActiveScreen
            || lastWindowWidth != state.WindowWidth
            || lastWindowHeight != state.WindowHeight;

        lastScreen = state.ActiveScreen;
        lastWindowWidth = state.WindowWidth;
        lastWindowHeight = state.WindowHeight;
        invalidated = false;
        return requiresFullRedraw;
    }
}
