namespace Tarn.ClientApp.Play.Screens.MatchCenter;

public static class MatchReplayNavigator
{
    public static RoundSnapshotViewModel GetCurrentSnapshot(MatchCenterState state)
    {
        var replay = state.Replay ?? throw new InvalidOperationException("Cannot resolve a replay snapshot without replay data.");
        return GetCurrentSnapshot(replay, state.CurrentEventIndex);
    }

    public static RoundSnapshotViewModel GetCurrentSnapshot(MatchReplayViewModel replay, int currentEventIndex)
    {
        if (replay.RoundSnapshots.Count == 0)
        {
            throw new InvalidOperationException("Replay data must include at least one snapshot.");
        }

        return replay.RoundSnapshots
            .FirstOrDefault(snapshot => snapshot.LastLogIndexExclusive >= currentEventIndex)
            ?? replay.RoundSnapshots.Last();
    }

    public static void AdvanceEvent(MatchCenterState state)
    {
        if (state.Replay is null || state.Replay.EventLog.Count == 0)
        {
            return;
        }

        state.CurrentEventIndex = Math.Min(state.Replay.EventLog.Count - 1, state.CurrentEventIndex + 1);
    }

    public static void AdvanceRound(MatchCenterState state)
    {
        if (state.Replay is null || state.Replay.RoundSnapshots.Count == 0)
        {
            return;
        }

        var next = state.Replay.RoundSnapshots.FirstOrDefault(snapshot => snapshot.LastLogIndexExclusive > state.CurrentEventIndex + 1);
        if (next is null)
        {
            return;
        }

        state.CurrentEventIndex = Math.Min(Math.Max(0, state.Replay.EventLog.Count - 1), Math.Max(0, next.LastLogIndexExclusive - 1));
    }

    public static bool IsReplayComplete(MatchCenterState state)
    {
        return state.Replay is null || state.Replay.EventLog.Count == 0 || state.CurrentEventIndex >= state.Replay.EventLog.Count - 1;
    }
}
