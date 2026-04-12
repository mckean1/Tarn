namespace Tarn.ClientApp.Play.Screens.Schedule;

public sealed class ScheduleState
{
    public int SelectedWeek { get; set; }
    public int SelectedFixtureIndex { get; set; }
    public string FocusLeagueLabel { get; set; } = string.Empty;
    public IReadOnlyList<ScheduleFixtureItem> Fixtures { get; set; } = [];
    public ScheduleFixtureDetail? Detail { get; set; }
}

public sealed record ScheduleFixtureItem(
    string MatchId,
    string LeagueLabel,
    string Pairing,
    string Status,
    string Result,
    bool ReplayAvailable,
    bool IsPlayerFixture);

public sealed record ScheduleFixtureDetail(
    string MatchId,
    string Title,
    IReadOnlyList<string> Lines,
    bool ReplayAvailable);
