namespace Tarn.ClientApp.Play.Screens.WeekSummary;

public sealed class WeekSummaryState
{
    public WeekSummaryViewModel? Summary { get; set; }
    public int SelectedActionIndex { get; set; }
}

public sealed record WeekSummaryViewModel(
    string Title,
    IReadOnlyList<string> Lines,
    string? ReplayMatchId,
    IReadOnlyList<string> Actions);
