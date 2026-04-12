using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Screens.League;

public sealed class LeagueController : IPlayScreenController
{
    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        switch (action)
        {
            case InputAction.MoveUp:
                state.League.SelectedIndex = ScreenSelection.Move(state.League.SelectedIndex, state.League.ViewModel?.Rows.Count ?? 0, -1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveDown:
                state.League.SelectedIndex = ScreenSelection.Move(state.League.SelectedIndex, state.League.ViewModel?.Rows.Count ?? 0, 1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveLeft:
                state.League.SelectedLeagueOffset--;
                state.League.SelectedIndex = 0;
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveRight:
                state.League.SelectedLeagueOffset++;
                state.League.SelectedIndex = 0;
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.Back:
                return new ScreenControllerResult { NavigateTo = ScreenId.Dashboard };
            default:
                return ScreenControllerResult.None;
        }
    }
}
