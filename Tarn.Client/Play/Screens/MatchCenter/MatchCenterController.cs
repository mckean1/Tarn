using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Screens.MatchCenter;

public sealed class MatchCenterController : IPlayScreenController
{
    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        var replay = state.MatchCenter.Replay;
        switch (action)
        {
            case InputAction.NextEvent:
                if (replay is not null && replay.EventLog.Count > 0)
                {
                    state.MatchCenter.CurrentEventIndex = Math.Min(replay.EventLog.Count - 1, state.MatchCenter.CurrentEventIndex + 1);
                }
                return ScreenControllerResult.None;
            case InputAction.NextRound:
                if (replay is not null && replay.RoundSnapshots.Count > 0)
                {
                    var next = replay.RoundSnapshots.FirstOrDefault(snapshot => snapshot.LastLogIndexExclusive > state.MatchCenter.CurrentEventIndex + 1);
                    if (next is not null)
                    {
                        state.MatchCenter.CurrentEventIndex = Math.Min(Math.Max(0, replay.EventLog.Count - 1), Math.Max(0, next.LastLogIndexExclusive - 1));
                    }
                }
                return ScreenControllerResult.None;
            case InputAction.ToggleAutoplay:
                state.MatchCenter.AutoplayEnabled = !state.MatchCenter.AutoplayEnabled;
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
