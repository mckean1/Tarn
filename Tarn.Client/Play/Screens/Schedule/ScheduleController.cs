using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Screens.Schedule;

public sealed class ScheduleController : IPlayScreenController
{
    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        switch (action)
        {
            case InputAction.MoveUp:
                state.Schedule.SelectedFixtureIndex = ScreenSelection.Move(state.Schedule.SelectedFixtureIndex, state.Schedule.Fixtures.Count, -1);
                return ScreenControllerResult.None;
            case InputAction.MoveDown:
                state.Schedule.SelectedFixtureIndex = ScreenSelection.Move(state.Schedule.SelectedFixtureIndex, state.Schedule.Fixtures.Count, 1);
                return ScreenControllerResult.None;
            case InputAction.MoveLeft:
                state.Schedule.SelectedWeek = RefreshService.ClampWeek(state.Schedule.SelectedWeek - 1, state.World);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveRight:
                state.Schedule.SelectedWeek = RefreshService.ClampWeek(state.Schedule.SelectedWeek + 1, state.World);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.Select:
                if (state.Schedule.Detail?.ReplayAvailable == true)
                {
                    return ReplayNavigation.OpenReplay(state, ScreenId.Schedule, state.Schedule.Detail.MatchId);
                }

                return new ScreenControllerResult
                {
                    Message = new MessageBarState(MessageSeverity.Info, "Replay will unlock after the fixture is complete."),
                };
            case InputAction.Back:
                return new ScreenControllerResult { NavigateTo = ScreenId.Dashboard };
            default:
                return ScreenControllerResult.None;
        }
    }
}
