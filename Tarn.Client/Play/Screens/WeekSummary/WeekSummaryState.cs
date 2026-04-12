namespace Tarn.ClientApp.Play.Screens.WeekSummary;

public sealed class WeekSummaryState
{
    public WeekSummaryViewModel? Summary { get; set; }
    public int SelectedActionIndex { get; set; }
}

public sealed record WeekSummaryViewModel(
    string Title,
    string Subtitle,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Notes,
    bool IsGenerated,
    string? ReplayMatchId,
    IReadOnlyList<string> Actions);
