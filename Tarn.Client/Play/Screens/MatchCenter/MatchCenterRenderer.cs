using System.Text;
using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.MatchCenter;

public static class MatchCenterRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var replay = state.MatchCenter.Replay;
        var layout = new Layout(body.Width, body.Height + Layout.HeaderHeight + Layout.MessageBarHeight + Layout.FooterHeight);
        var builder = new StringBuilder();
        builder.AppendLine("Match Center");
        builder.AppendLine($"Autoplay: {(state.MatchCenter.AutoplayEnabled ? "On" : "Off")}");
        builder.AppendLine();

        if (replay is null)
        {
            builder.AppendLine(ScreenText.EmptyState("No Replay", "Finish a fixture, then open it from Schedule or Week Summary.", body.Width));
            return ScreenText.FitBlock(builder.ToString(), body.Width, body.Height);
        }

        var snapshot = replay.RoundSnapshots
            .Where(item => item.LastLogIndexExclusive > state.MatchCenter.CurrentEventIndex)
            .DefaultIfEmpty(replay.RoundSnapshots.Last())
            .First();

        builder.AppendLine(replay.Title);
        builder.AppendLine($"{ScreenText.StatusChip("Initiative")} {replay.Initiative}");
        builder.AppendLine($"{ScreenText.StatusChip(snapshot.BattleStateLabel)} {ScreenText.StatusChip("Result")} {replay.Result}");
        builder.AppendLine($"{snapshot.PlayerOne.Label}: HP {snapshot.PlayerOne.Health} | Fatigue {snapshot.PlayerOne.Fatigue}");
        builder.AppendLine($"{snapshot.PlayerTwo.Label}: HP {snapshot.PlayerTwo.Health} | Fatigue {snapshot.PlayerTwo.Fatigue}");
        builder.AppendLine("Battlefield");
        foreach (var line in snapshot.BattlefieldLines)
        {
            builder.AppendLine(line);
        }

        if (!layout.IsVeryNarrow)
        {
            builder.AppendLine("Counters");
            foreach (var line in snapshot.CounterLines)
            {
                builder.AppendLine(line);
            }
        }

        builder.AppendLine("Event Log");
        var logWindow = layout.IsVeryNarrow ? Math.Max(4, body.Height - 12) : Math.Max(6, body.Height - 14);
        var start = Math.Max(0, state.MatchCenter.CurrentEventIndex - (logWindow / 2));
        var end = Math.Min(replay.EventLog.Count, start + logWindow);
        for (var index = start; index < end; index++)
        {
            var marker = index == state.MatchCenter.CurrentEventIndex ? ">>" : "  ";
            builder.AppendLine($"{marker} {Layout.Truncate(replay.EventLog[index], body.Width - 3)}");
        }

        return ScreenText.FitBlock(builder.ToString(), body.Width, body.Height);
    }
}
