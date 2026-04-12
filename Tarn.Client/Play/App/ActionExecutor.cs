using Tarn.Domain;
using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.App;

public sealed class ActionExecutor
{
    private readonly RefreshService refreshService;
    private readonly WorldSimulator simulator = new();
    public ActionExecutor(RefreshService refreshService)
    {
        this.refreshService = refreshService;
    }

    public void ExecutePending(AppState state)
    {
        if (state.Modal?.PendingAction is null)
        {
            return;
        }

        var action = state.Modal.PendingAction;
        state.Modal = null;

        if (action.Kind == PendingActionKind.AdvanceWeek)
        {
            ExecuteAdvanceWeek(state);
            return;
        }

        if (action.Kind == PendingActionKind.AutoBuildDeck)
        {
            ExecuteAutoBuildDeck(state);
            return;
        }

        if (action.Kind == PendingActionKind.BuyCollectorSingle)
        {
            ExecuteBuyCollectorSingle(state, action.ReferenceId!);
            return;
        }

        if (action.Kind == PendingActionKind.BuyCollectorPack)
        {
            ExecuteBuyCollectorPack(state, action.ReferenceId!);
            return;
        }

        if (action.Kind == PendingActionKind.SellToCollector)
        {
            ExecuteSellToCollector(state, action.ReferenceId!);
            return;
        }

        if (action.Kind == PendingActionKind.PlaceMarketBid)
        {
            ExecutePlaceMarketBid(state, action.ReferenceId!, action.NumericValue);
            return;
        }

        if (action.Kind == PendingActionKind.CreateMarketListing)
        {
            ExecuteCreateMarketListing(state, action.ReferenceId!, action.NumericValue);
        }
    }

    private void ExecuteAdvanceWeek(AppState state)
    {
        var previousYear = state.World.Season.Year;
        var previousWeek = state.World.Season.CurrentWeek;
        var previousCash = state.HumanPlayer.Cash;
        var previousStanding = state.World.Season.Standings[state.HumanPlayerId];
        var previousRank = StandingsCalculator.Rank(state.World.Season.Standings.Values.Where(entry => entry.League == state.HumanPlayer.League).ToList())
            .First(entry => entry.DeckId == state.HumanPlayerId)
            .LeagueRank;
        var seed = (state.World.Season.Year * 1000) + state.World.Season.CurrentWeek;

        simulator.StepWeek(state.World, seed);
        WorldStorage.Save(state.World, state.StoragePath);
        state.World = WorldStorage.Load(state.StoragePath);
        refreshService.RefreshAll(state);
        refreshService.PopulateWeekSummary(state, previousYear, previousWeek, previousCash, previousStanding.Wins, previousStanding.Losses, previousRank);
        NavigationService.Navigate(state, ScreenId.WeekSummary);
        state.MessageBar = new MessageBarState(MessageSeverity.Success, $"Advanced to Year {state.World.Season.Year}, Week {state.World.Season.CurrentWeek}.");
    }

    private void ExecuteAutoBuildDeck(AppState state)
    {
        var player = state.HumanPlayer;
        if (!DeckBuilder.TryBuildBestDeck(state.World, player, out var deck) || deck is null)
        {
            state.MessageBar = new MessageBarState(MessageSeverity.Error, "Could not build a legal deck from your collection.");
            return;
        }

        player.ActiveDeck = deck;
        WorldStorage.Save(state.World, state.StoragePath);
        state.World = WorldStorage.Load(state.StoragePath);
        refreshService.RefreshAll(state);
        state.MessageBar = new MessageBarState(MessageSeverity.Success, "Built and saved a best legal deck.");
    }

    private void ExecuteBuyCollectorSingle(AppState state, string listingId)
    {
        if (!CollectorService.BuySingle(state.World, state.HumanPlayerId, listingId))
        {
            state.MessageBar = new MessageBarState(MessageSeverity.Error, "Could not buy that single.");
            return;
        }

        SaveAndReload(state);
        state.MessageBar = new MessageBarState(MessageSeverity.Success, "Bought a card from the Collector.");
    }

    private void ExecuteBuyCollectorPack(AppState state, string productId)
    {
        var beforeIds = state.HumanPlayer.Collection.Select(card => card.CardId).ToHashSet(StringComparer.Ordinal);
        var cards = CollectorService.BuyPack(state.World, state.HumanPlayerId, productId, (state.World.Season.Year * 1000) + state.World.Season.CurrentWeek + 17);
        if (cards.Count == 0)
        {
            state.MessageBar = new MessageBarState(MessageSeverity.Error, "Could not open that pack.");
            return;
        }

        var revealCards = cards
            .Select(card =>
            {
                var definition = state.World.GetLatestDefinition(card.CardId);
                return new PackRevealCard(definition.Id, definition.Name, definition.Type, definition.Rarity, !beforeIds.Contains(card.CardId));
            })
            .ToList();

        SaveAndReload(state);
        state.Modal = new ModalState
        {
            Kind = ModalKind.PackReveal,
            Title = "Pack Reveal",
            Lines = CollectorQueries.FormatPackReveal(revealCards).Split(Environment.NewLine),
        };
        state.MessageBar = new MessageBarState(MessageSeverity.Success, "Pack opened.");
    }

    private void ExecuteSellToCollector(AppState state, string instanceId)
    {
        if (!CollectorService.SellToCollector(state.World, state.HumanPlayerId, instanceId))
        {
            state.MessageBar = new MessageBarState(MessageSeverity.Error, "Could not sell that card.");
            return;
        }

        SaveAndReload(state);
        state.MessageBar = new MessageBarState(MessageSeverity.Success, "Sold a card to the Collector.");
    }

    private void ExecutePlaceMarketBid(AppState state, string listingId, int amount)
    {
        if (!MarketService.PlaceBid(state.World, state.HumanPlayerId, listingId, amount))
        {
            state.MessageBar = new MessageBarState(MessageSeverity.Error, "Could not place that bid.");
            return;
        }

        SaveAndReload(state);
        state.MessageBar = new MessageBarState(MessageSeverity.Success, $"Bid placed for {amount}.");
    }

    private void ExecuteCreateMarketListing(AppState state, string instanceId, int minimumBid)
    {
        var listing = MarketService.CreateAuctionListing(state.World, state.HumanPlayerId, instanceId, minimumBid);
        if (listing is null)
        {
            state.MessageBar = new MessageBarState(MessageSeverity.Error, "Could not create that listing.");
            return;
        }

        SaveAndReload(state);
        state.MessageBar = new MessageBarState(MessageSeverity.Success, $"Created listing {listing.Id}.");
    }

    private void SaveAndReload(AppState state)
    {
        WorldStorage.Save(state.World, state.StoragePath);
        state.World = WorldStorage.Load(state.StoragePath);
        refreshService.RefreshAll(state);
    }
}
