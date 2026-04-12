namespace Tarn.ClientApp.Play.App;

public static class ReplayNavigation
{
    public static ScreenControllerResult OpenReplay(AppState state, ScreenId sourceScreen, string matchId)
    {
        state.MatchCenter.MatchId = matchId;
        state.MatchCenter.CurrentEventIndex = 0;
        state.MatchCenter.ReturnScreen = sourceScreen;
        return new ScreenControllerResult
        {
            NavigateTo = ScreenId.MatchCenter,
            RequiresRefresh = true,
        };
    }
}
