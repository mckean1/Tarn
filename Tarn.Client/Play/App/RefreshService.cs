using Tarn.ClientApp.Play.Queries;
using Tarn.Domain;

namespace Tarn.ClientApp.Play.App;

public sealed class RefreshService
{
    private readonly DashboardQueries dashboardQueries = new();
    private readonly ScheduleQueries scheduleQueries = new();
    private readonly MatchReplayQueries matchReplayQueries = new();
    private readonly WeekSummaryQueries weekSummaryQueries = new();
    private readonly LeagueQueries leagueQueries = new();
    private readonly CollectionQueries collectionQueries = new();
    private readonly DeckQueries deckQueries = new();
    private readonly CollectorQueries collectorQueries = new();
    private readonly MarketQueries marketQueries = new();

    public AppState CreateInitialState(string storagePath, World world)
    {
        var human = world.Players.Values.Single(player => player.IsHuman);
        var state = new AppState
        {
            StoragePath = storagePath,
            World = world,
            HumanPlayerId = human.Id,
        };

        RefreshAll(state);
        state.MessageBar = new MessageBarState(MessageSeverity.Info, "Welcome to Tarn play mode.");
        return state;
    }

    public void RefreshAll(AppState state)
    {
        RefreshDashboard(state);
        RefreshSchedule(state);
        RefreshMatchCenter(state);
        RefreshWeekSummaryDefaults(state);
        RefreshLeague(state);
        RefreshCollection(state);
        RefreshDeck(state);
        RefreshCollector(state);
        RefreshMarket(state);
    }

    public void RefreshDashboard(AppState state)
    {
        state.Dashboard.ViewModel = dashboardQueries.Build(state.World, state.HumanPlayerId);
        state.Dashboard.SelectedActionIndex = Math.Clamp(state.Dashboard.SelectedActionIndex, 0, state.Dashboard.ViewModel.RecommendedActions.Count - 1);
    }

    public void RefreshSchedule(AppState state)
    {
        var world = state.World;
        state.Schedule.SelectedWeek = ClampWeek(state.Schedule.SelectedWeek == 0 ? world.Season.CurrentWeek : state.Schedule.SelectedWeek, world);
        var viewModel = scheduleQueries.Build(world, state.Schedule.SelectedWeek, state.Schedule.SelectedFixtureIndex, state.HumanPlayerId);
        state.Schedule.SelectedWeek = viewModel.SelectedWeek;
        state.Schedule.SelectedFixtureIndex = viewModel.SelectedFixtureIndex;
        state.Schedule.Fixtures = viewModel.Fixtures;
        state.Schedule.Detail = viewModel.Detail;
    }

    public void RefreshMatchCenter(AppState state)
    {
        if (string.IsNullOrEmpty(state.MatchCenter.MatchId))
        {
            state.MatchCenter.MatchId = state.World.Season.Schedule
                .Where(match => match.Result is not null && (match.HomePlayerId == state.HumanPlayerId || match.AwayPlayerId == state.HumanPlayerId))
                .OrderByDescending(match => match.Week)
                .ThenByDescending(match => match.FixturePriority)
                .Select(match => match.Id)
                .FirstOrDefault();
        }

        state.MatchCenter.Replay = string.IsNullOrEmpty(state.MatchCenter.MatchId) ? null : matchReplayQueries.Build(state.World, state.MatchCenter.MatchId);
        state.MatchCenter.CurrentEventIndex = state.MatchCenter.Replay is null
            ? 0
            : Math.Clamp(state.MatchCenter.CurrentEventIndex, 0, Math.Max(0, state.MatchCenter.Replay.EventLog.Count - 1));
    }

    public void RefreshWeekSummaryDefaults(AppState state)
    {
        if (state.WeekSummary.Summary is not null)
        {
            return;
        }

        state.WeekSummary.Summary = weekSummaryQueries.BuildDefault();
    }

    public void PopulateWeekSummary(AppState state, int previousYear, int previousWeek, int previousCash, int previousWins, int previousLosses, int previousRank)
    {
        state.WeekSummary.Summary = weekSummaryQueries.BuildAfterAdvance(
            state.World,
            state.HumanPlayerId,
            previousYear,
            previousWeek,
            previousCash,
            previousWins,
            previousLosses,
            previousRank);
        state.WeekSummary.SelectedActionIndex = 0;
    }

    public static int ClampWeek(int week, World world) =>
        Math.Clamp(week, 1, world.Config.Season.TotalWeeks);

    public void RefreshLeague(AppState state)
    {
        state.League.ViewModel = leagueQueries.Build(state.World, state.HumanPlayerId, state.League.SelectedLeagueOffset, state.League.SelectedIndex);
        state.League.SelectedLeagueOffset = state.League.ViewModel.SelectedLeagueOffset;
        state.League.SelectedIndex = state.League.ViewModel.SelectedIndex;
    }

    public void RefreshCollection(AppState state)
    {
        state.Collection.ViewModel = collectionQueries.Build(state.World, state.HumanPlayerId, state.Collection.Filter, state.Collection.Sort, state.Collection.SelectedIndex);
        state.Collection.SelectedIndex = state.Collection.ViewModel.SelectedIndex;
    }

    public void RefreshDeck(AppState state)
    {
        state.Deck.ViewModel = deckQueries.Build(state.World, state.HumanPlayerId, state.Deck.SelectedIndex);
        state.Deck.SelectedIndex = state.Deck.ViewModel.SelectedIndex;
    }

    public void RefreshCollector(AppState state)
    {
        state.Collector.ViewModel = collectorQueries.Build(state.World, state.HumanPlayerId, state.Collector.Tab, state.Collector.SelectedIndex);
        state.Collector.SelectedIndex = state.Collector.ViewModel.SelectedIndex;
    }

    public void RefreshMarket(AppState state)
    {
        state.Market.ViewModel = marketQueries.Build(
            state.World,
            state.HumanPlayerId,
            state.Market.Tab,
            state.Market.SelectedIndex,
            state.Market.ProposedBidOrPrice,
            state.Market.ProposedDurationWeeks);
        state.Market.SelectedIndex = state.Market.ViewModel.SelectedIndex;
        state.Market.ProposedBidOrPrice = state.Market.ViewModel.ProposedBidOrPrice;
        state.Market.ProposedDurationWeeks = state.Market.ViewModel.ProposedDurationWeeks;
    }
}
