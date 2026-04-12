using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Screens.MatchCenter;

public sealed class MatchCenterState
{
    public string? MatchId { get; set; }
    public ScreenId? ReturnScreen { get; set; }
    public int CurrentEventIndex { get; set; }
    public bool AutoplayEnabled { get; set; }
    public MatchReplayViewModel? Replay { get; set; }
}

public sealed record MatchReplayViewModel(
    string MatchId,
    string Title,
    string Initiative,
    string Result,
    IReadOnlyList<string> EventLog,
    IReadOnlyList<RoundSnapshotViewModel> RoundSnapshots);

public sealed record RoundSnapshotViewModel(
    int RoundNumber,
    string BattleStateLabel,
    ChampionPanelViewModel PlayerOne,
    ChampionPanelViewModel PlayerTwo,
    IReadOnlyList<string> BattlefieldLines,
    IReadOnlyList<string> CounterLines,
    int LastLogIndexExclusive);

public sealed record ChampionPanelViewModel(string Label, int Health, int Fatigue);
