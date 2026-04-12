using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Screens.Dashboard;

public sealed class DashboardController : IPlayScreenController
{
    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        var actionCount = state.Dashboard.ViewModel?.RecommendedActions.Count ?? 0;
        switch (action)
        {
            case InputAction.MoveUp:
                state.Dashboard.SelectedActionIndex = ScreenSelection.Move(state.Dashboard.SelectedActionIndex, actionCount, -1);
                return ScreenControllerResult.None;
            case InputAction.MoveDown:
                state.Dashboard.SelectedActionIndex = ScreenSelection.Move(state.Dashboard.SelectedActionIndex, actionCount, 1);
                return ScreenControllerResult.None;
            case InputAction.Select:
                return HandleSelection(state);
            case InputAction.AdvanceWeek:
                return BuildAdvanceWeekModal();
            default:
                return ScreenControllerResult.None;
        }
    }

    private static ScreenControllerResult HandleSelection(AppState state)
    {
        return state.Dashboard.SelectedActionIndex switch
        {
            0 => new ScreenControllerResult { NavigateTo = ScreenId.Schedule },
            1 => BuildAdvanceWeekModal(),
            2 => new ScreenControllerResult { NavigateTo = ScreenId.MatchCenter },
            3 => new ScreenControllerResult { NavigateTo = ScreenId.WeekSummary },
            4 => new ScreenControllerResult { NavigateTo = ScreenId.Collector, RequiresRefresh = true },
            5 => new ScreenControllerResult { NavigateTo = ScreenId.Market, RequiresRefresh = true },
            _ => ScreenControllerResult.None,
        };
    }

    private static ScreenControllerResult BuildAdvanceWeekModal()
    {
        return new ScreenControllerResult
        {
            Modal = new ModalState
            {
                Kind = ModalKind.Confirmation,
                Title = "Advance Week?",
                Lines =
                [
                    "Advance the world simulation by one week.",
                    "This will save the world and refresh the UI.",
                ],
                PendingAction = new PendingAction(PendingActionKind.AdvanceWeek, "Advance Week", "Simulate the next week."),
            },
        };
    }
}
