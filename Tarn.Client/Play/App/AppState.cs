using Tarn.ClientApp.Play.Screens.Dashboard;
using Tarn.ClientApp.Play.Screens.Deck;
using Tarn.ClientApp.Play.Screens.Collection;
using Tarn.ClientApp.Play.Screens.Collector;
using Tarn.ClientApp.Play.Screens.League;
using Tarn.ClientApp.Play.Screens.Market;
using Tarn.ClientApp.Play.Screens.MatchCenter;
using Tarn.ClientApp.Play.Screens.Schedule;
using Tarn.ClientApp.Play.Screens.WeekSummary;
using Tarn.Domain;
using LeagueScreenState = Tarn.ClientApp.Play.Screens.League.LeagueState;

namespace Tarn.ClientApp.Play.App;

public sealed class AppState
{
    public required World World { get; set; }
    public required string StoragePath { get; init; }
    public required string HumanPlayerId { get; init; }
    public ScreenId ActiveScreen { get; set; } = ScreenId.Dashboard;
    public ScreenId PreviousScreen { get; set; } = ScreenId.Dashboard;
    public bool ShouldQuit { get; set; }
    public int WindowWidth { get; set; } = 120;
    public int WindowHeight { get; set; } = 40;
    public MessageBarState? MessageBar { get; set; }
    public bool IsNarrowLayout { get; set; }
    public ModalState? Modal { get; set; }
    public DashboardState Dashboard { get; } = new();
    public ScheduleState Schedule { get; } = new();
    public MatchCenterState MatchCenter { get; } = new();
    public WeekSummaryState WeekSummary { get; } = new();
    public LeagueScreenState League { get; } = new();
    public CollectionState Collection { get; } = new();
    public DeckState Deck { get; } = new();
    public CollectorState Collector { get; } = new();
    public MarketState Market { get; } = new();

    public Player HumanPlayer => World.Players[HumanPlayerId];
}
