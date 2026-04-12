namespace Tarn.ClientApp.Play.Screens.Schedule;

public sealed class ScheduleState
{
    public int SelectedWeek { get; set; }
    public int SelectedFixtureIndex { get; set; }
    public IReadOnlyList<ScheduleFixtureItem> Fixtures { get; set; } = [];
    public ScheduleFixtureDetail? Detail { get; set; }
}

public sealed record ScheduleFixtureItem(string MatchId, string Summary, bool ReplayAvailable);

public sealed record ScheduleFixtureDetail(
    string MatchId,
    string Title,
    IReadOnlyList<string> Lines,
    bool ReplayAvailable);
