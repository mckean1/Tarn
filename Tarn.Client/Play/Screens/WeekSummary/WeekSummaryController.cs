using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Screens.WeekSummary;

public sealed class WeekSummaryController : IPlayScreenController
{
    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        var summary = state.WeekSummary.Summary;
        return action switch
        {
            InputAction.MoveUp => Move(state, -1),
            InputAction.MoveDown => Move(state, 1),
            InputAction.Select when summary?.ReplayMatchId is not null && state.WeekSummary.SelectedActionIndex == 0 => OpenReplay(state, summary.ReplayMatchId),
            InputAction.Select or InputAction.Back => new ScreenControllerResult { NavigateTo = ScreenId.Dashboard },
            _ => ScreenControllerResult.None,
        };
    }

    private static ScreenControllerResult Move(AppState state, int delta)
    {
        var count = state.WeekSummary.Summary?.Actions.Count ?? 1;
        state.WeekSummary.SelectedActionIndex = ScreenSelection.Move(state.WeekSummary.SelectedActionIndex, count, delta);
        return ScreenControllerResult.None;
    }

    private static ScreenControllerResult OpenReplay(AppState state, string matchId)
    {
        return ReplayNavigation.OpenReplay(state, ScreenId.WeekSummary, matchId);
    }
}
