using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Screens.MatchCenter;

public sealed class MatchCenterController : IPlayScreenController
{
    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        switch (action)
        {
            case InputAction.NextEvent:
                MatchReplayNavigator.AdvanceEvent(state.MatchCenter);
                return ScreenControllerResult.None;
            case InputAction.NextRound:
                MatchReplayNavigator.AdvanceRound(state.MatchCenter);
                return ScreenControllerResult.None;
            case InputAction.ToggleAutoplay:
                state.MatchCenter.AutoplayEnabled = !state.MatchCenter.AutoplayEnabled && !MatchReplayNavigator.IsReplayComplete(state.MatchCenter);
                return new ScreenControllerResult
                {
                    Message = new MessageBarState(MessageSeverity.Info, state.MatchCenter.AutoplayEnabled ? "Autoplay enabled." : "Autoplay paused."),
                };
            case InputAction.Back:
                return new ScreenControllerResult { NavigateTo = state.MatchCenter.ReturnScreen ?? state.PreviousScreen };
            default:
                return ScreenControllerResult.None;
        }
    }
}
