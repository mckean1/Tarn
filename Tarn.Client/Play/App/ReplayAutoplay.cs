using System.Threading;
using Tarn.ClientApp.Play.Screens.MatchCenter;

namespace Tarn.ClientApp.Play.App;

public static class ReplayAutoplay
{
    public const int TickDelayMilliseconds = 150;

    public static bool ShouldPoll(AppState state)
    {
        return state.ActiveScreen == ScreenId.MatchCenter
            && state.Modal is null
            && state.MatchCenter.AutoplayEnabled
            && state.MatchCenter.Replay is not null
            && !MatchReplayNavigator.IsReplayComplete(state.MatchCenter);
    }

    public static bool AdvanceTick(AppState state)
    {
        if (!ShouldPoll(state))
        {
            if (state.MatchCenter.AutoplayEnabled && MatchReplayNavigator.IsReplayComplete(state.MatchCenter))
            {
                state.MatchCenter.AutoplayEnabled = false;
            }

            return false;
        }

        MatchReplayNavigator.AdvanceEvent(state.MatchCenter);
        if (MatchReplayNavigator.IsReplayComplete(state.MatchCenter))
        {
            state.MatchCenter.AutoplayEnabled = false;
        }

        return true;
    }

    public static bool WaitForInputOrTick(AppState state)
    {
        if (!ShouldPoll(state))
        {
            return true;
        }

        var remaining = TickDelayMilliseconds;
        while (remaining > 0)
        {
            if (IsKeyAvailable())
            {
                return true;
            }

            var sleep = Math.Min(25, remaining);
            Thread.Sleep(sleep);
            remaining -= sleep;
        }

        return IsKeyAvailable();
    }

    private static bool IsKeyAvailable()
    {
        try
        {
            return Console.KeyAvailable;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
