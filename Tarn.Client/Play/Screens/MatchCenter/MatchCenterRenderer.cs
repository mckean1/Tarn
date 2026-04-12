using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.MatchCenter;

public static class MatchCenterRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var replay = state.MatchCenter.Replay;
        if (replay is null)
        {
            return ScreenText.FitBlock(
                ScreenText.EmptyState("No Replay", "Finish a fixture, then open it from Schedule or Week Summary.", body.Width),
                body.Width,
                body.Height);
        }

        var snapshot = MatchReplayNavigator.GetCurrentSnapshot(state.MatchCenter);
        var summaryHeight = Math.Clamp(body.Height >= 16 ? 5 : 4, 4, Math.Max(4, body.Height));
        var (summaryRect, detailRect) = body.SplitRows(summaryHeight);
        var summaryBox = BoxDrawing.RenderBox("Battle Summary", BuildSummaryLines(state, replay, snapshot, summaryRect.GetInnerRect()), summaryRect.Width, summaryRect.Height);
        var detailLines = RenderDetailZones(state, replay, snapshot, detailRect);
        return string.Join(Environment.NewLine, summaryBox.Concat(detailLines).Take(body.Height));
    }

    private static IReadOnlyList<string> RenderDetailZones(AppState state, MatchReplayViewModel replay, RoundSnapshotViewModel snapshot, Rect rect)
    {
        if (rect.Height <= 0)
        {
            return [];
        }

        return rect.Width >= 96
            ? RenderThreeZoneLayout(state, replay, snapshot, rect)
            : rect.Width >= 68
                ? RenderTwoZoneLayout(state, replay, snapshot, rect)
                : RenderStackedLayout(state, replay, snapshot, rect);
    }

    private static IReadOnlyList<string> RenderThreeZoneLayout(AppState state, MatchReplayViewModel replay, RoundSnapshotViewModel snapshot, Rect rect)
    {
        const int gap = 2;
        var leftWidth = Math.Max(28, ((rect.Width - (gap * 2)) * 6) / 23);
        var centerWidth = Math.Max(32, ((rect.Width - (gap * 2)) * 9) / 23);
        var (left, remainder) = rect.SplitColumns(leftWidth, gap);
        var (center, right) = remainder.SplitColumns(centerWidth, gap);

        var leftBox = BoxDrawing.RenderBox("Combatants", BuildCombatantLines(replay, snapshot, left.GetInnerRect()), left.Width, left.Height);
        var centerBox = BoxDrawing.RenderBox("Battlefield", BuildBattlefieldLines(replay, snapshot, center.GetInnerRect()), center.Width, center.Height);
        var rightBox = BoxDrawing.RenderBox($"Event Log · {FormatReplayPosition(state.MatchCenter.CurrentEventIndex, replay.EventLog.Count)}", BuildEventLogLines(state, replay, right.GetInnerRect()), right.Width, right.Height);
        return BoxDrawing.MergeColumns(BoxDrawing.MergeColumns(leftBox, centerBox, gap), rightBox, gap);
    }

    private static IReadOnlyList<string> RenderTwoZoneLayout(AppState state, MatchReplayViewModel replay, RoundSnapshotViewModel snapshot, Rect rect)
    {
        const int gap = 1;
        var topHeight = Math.Max(6, rect.Height / 2);
        var (top, bottom) = rect.SplitRows(topHeight, gap);
        var leftWidth = Math.Max(28, (top.Width - gap) / 2);
        var (left, right) = top.SplitColumns(leftWidth, gap);
        var topLines = BoxDrawing.MergeColumns(
            BoxDrawing.RenderBox("Combatants", BuildCombatantLines(replay, snapshot, left.GetInnerRect()), left.Width, left.Height),
            BoxDrawing.RenderBox("Battlefield", BuildBattlefieldLines(replay, snapshot, right.GetInnerRect()), right.Width, right.Height),
            gap);
        var bottomLines = BoxDrawing.RenderBox($"Event Log · {FormatReplayPosition(state.MatchCenter.CurrentEventIndex, replay.EventLog.Count)}", BuildEventLogLines(state, replay, bottom.GetInnerRect()), bottom.Width, bottom.Height);
        return topLines.Concat(bottomLines).ToList();
    }

    private static IReadOnlyList<string> RenderStackedLayout(AppState state, MatchReplayViewModel replay, RoundSnapshotViewModel snapshot, Rect rect)
    {
        var sectionCount = 3;
        var minimumSectionHeight = 3;
        var sectionHeight = Math.Max(minimumSectionHeight, rect.Height / sectionCount);
        var firstHeight = Math.Clamp(sectionHeight, minimumSectionHeight, Math.Max(minimumSectionHeight, rect.Height - (minimumSectionHeight * 2)));
        var (top, remaining) = rect.SplitRows(firstHeight);
        var secondHeight = Math.Clamp(sectionHeight, minimumSectionHeight, Math.Max(minimumSectionHeight, remaining.Height - minimumSectionHeight));
        var (middle, bottom) = remaining.SplitRows(secondHeight);
        return BoxDrawing.RenderBox("Combatants", BuildCombatantLines(replay, snapshot, top.GetInnerRect()), top.Width, top.Height)
            .Concat(BoxDrawing.RenderBox("Battlefield", BuildBattlefieldLines(replay, snapshot, middle.GetInnerRect()), middle.Width, middle.Height))
            .Concat(BoxDrawing.RenderBox($"Event Log · {FormatReplayPosition(state.MatchCenter.CurrentEventIndex, replay.EventLog.Count)}", BuildEventLogLines(state, replay, bottom.GetInnerRect()), bottom.Width, bottom.Height))
            .ToList();
    }

    private static IReadOnlyList<string> BuildSummaryLines(AppState state, MatchReplayViewModel replay, RoundSnapshotViewModel snapshot, Rect rect)
    {
        var lines = new List<string>
        {
            Layout.Truncate(replay.Title, rect.Width),
            Layout.Truncate($"Round: {snapshot.RoundLabel}  ·  Initiative: {snapshot.InitiativeLabel}  ·  State: {snapshot.BattleStateLabel}", rect.Width),
        };

        var replayStatus = $"Position: {FormatReplayPosition(state.MatchCenter.CurrentEventIndex, replay.EventLog.Count)}  ·  Autoplay: {(state.MatchCenter.AutoplayEnabled ? "On" : "Off")}";
        if (string.Equals(snapshot.BattleStateLabel, "Complete", StringComparison.Ordinal))
        {
            replayStatus += $"  ·  Result: {replay.Result}";
        }

        lines.Add(Layout.Truncate(replayStatus, rect.Width));
        return lines;
    }

    private static IReadOnlyList<string> BuildCombatantLines(MatchReplayViewModel replay, RoundSnapshotViewModel snapshot, Rect rect)
    {
        var lines = new List<string>();
        AppendCombatant(lines, snapshot.Home, snapshot.InitiativeLabel);
        lines.Add(string.Empty);
        AppendCombatant(lines, snapshot.Away, snapshot.InitiativeLabel);
        lines.Add(string.Empty);
        lines.Add(ScreenText.Secondary("Replay Info"));
        lines.AddRange(replay.ReplayInfoLines);
        lines.Add($"Initial initiative: {replay.Initiative}");
        if (string.Equals(snapshot.BattleStateLabel, "Complete", StringComparison.Ordinal))
        {
            lines.Add($"Result: {replay.Result}");
        }

        return lines.Take(Math.Max(1, rect.Height)).ToList();
    }

    private static void AppendCombatant(ICollection<string> lines, ChampionPanelViewModel panel, string initiativeLabel)
    {
        lines.Add($"{panel.SideLabel}: {panel.PlayerName}");
        lines.Add($"Champion: {panel.ChampionName}");
        lines.Add($"HP: {panel.Health}  ·  Fatigue: {panel.Fatigue}");
        if (string.Equals(panel.PlayerName, initiativeLabel, StringComparison.Ordinal))
        {
            lines.Add(ScreenText.StatusChip("INITIATIVE"));
        }
    }

    private static IReadOnlyList<string> BuildBattlefieldLines(MatchReplayViewModel replay, RoundSnapshotViewModel snapshot, Rect rect)
    {
        var lines = new List<string>
        {
            ScreenText.Secondary("Home board"),
        };
        lines.AddRange(snapshot.HomeBoardLines);
        lines.Add(string.Empty);
        lines.Add(ScreenText.Secondary("Away board"));
        lines.AddRange(snapshot.AwayBoardLines);
        lines.Add(string.Empty);
        lines.Add(ScreenText.Secondary("Counters"));
        lines.Add($"Home: {snapshot.HomeCounterSummary}");
        lines.Add($"Away: {snapshot.AwayCounterSummary}");
        if (string.Equals(snapshot.BattleStateLabel, "Complete", StringComparison.Ordinal))
        {
            lines.Add(string.Empty);
            lines.Add($"Result: {replay.Result}");
        }

        return lines.Take(Math.Max(1, rect.Height)).ToList();
    }

    private static IReadOnlyList<string> BuildEventLogLines(AppState state, MatchReplayViewModel replay, Rect rect)
    {
        if (replay.EventLog.Count == 0)
        {
            return ["Replay log unavailable."];
        }

        var visibleRows = Math.Max(1, rect.Height);
        var selectedIndex = Math.Clamp(state.MatchCenter.CurrentEventIndex, 0, replay.EventLog.Count - 1);
        var start = Math.Clamp(selectedIndex - (visibleRows / 2), 0, Math.Max(0, replay.EventLog.Count - visibleRows));
        var end = Math.Min(replay.EventLog.Count, start + visibleRows);
        var lines = new List<string>(end - start);
        for (var index = start; index < end; index++)
        {
            lines.Add(ScreenText.InteractiveRow(index == selectedIndex, replay.EventLog[index], selectedMarker: ">", unselectedMarker: " "));
        }

        return lines;
    }

    private static string FormatReplayPosition(int currentEventIndex, int eventCount)
    {
        if (eventCount <= 0)
        {
            return "0/0";
        }

        return $"{Math.Clamp(currentEventIndex + 1, 1, eventCount)}/{eventCount}";
    }
}
