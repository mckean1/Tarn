using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.League;

public sealed class LeagueState
{
    public int SelectedIndex { get; set; }
    public int SelectedLeagueOffset { get; set; }
    public LeagueViewModel? ViewModel { get; set; }
}
