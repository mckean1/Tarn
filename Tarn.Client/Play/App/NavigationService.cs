namespace Tarn.ClientApp.Play.App;

public static class NavigationService
{
    public static void Navigate(AppState state, ScreenId screen)
    {
        if (state.ActiveScreen == screen)
        {
            return;
        }

        state.PreviousScreen = state.ActiveScreen;
        state.ActiveScreen = screen;
    }

    public static void Back(AppState state)
    {
        var target = state.PreviousScreen;
        state.PreviousScreen = state.ActiveScreen;
        state.ActiveScreen = target;
    }
}
