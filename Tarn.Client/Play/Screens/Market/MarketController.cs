using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.Market;

public sealed class MarketController : IPlayScreenController
{
    private static readonly MarketTab[] Tabs = [MarketTab.Browse, MarketTab.MyListings, MarketTab.CreateListing];

    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        switch (action)
        {
            case InputAction.MoveUp:
                state.Market.SelectedIndex = ScreenSelection.Move(state.Market.SelectedIndex, state.Market.ViewModel?.Rows.Count ?? 0, -1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveDown:
                state.Market.SelectedIndex = ScreenSelection.Move(state.Market.SelectedIndex, state.Market.ViewModel?.Rows.Count ?? 0, 1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveLeft:
                state.Market.Tab = Cycle(state.Market.Tab, -1);
                state.Market.SelectedIndex = 0;
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveRight:
                state.Market.Tab = Cycle(state.Market.Tab, 1);
                state.Market.SelectedIndex = 0;
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.NextEvent:
                state.Market.ProposedBidOrPrice = Math.Max(1, state.Market.ProposedBidOrPrice - 1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.NextRound:
                state.Market.ProposedBidOrPrice = Math.Max(1, state.Market.ProposedBidOrPrice + 1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.Select:
                return BuildModal(state);
            case InputAction.Back:
                return new ScreenControllerResult { NavigateTo = ScreenId.Dashboard };
            default:
                return ScreenControllerResult.None;
        }
    }

    private static ScreenControllerResult BuildModal(AppState state)
    {
        var detail = state.Market.ViewModel?.Detail;
        if (detail is null)
        {
            return new ScreenControllerResult { Message = new MessageBarState(MessageSeverity.Info, "Nothing to act on in this tab.") };
        }

        return state.Market.Tab switch
        {
            MarketTab.Browse => Confirm("Place Bid?", $"Bid {state.Market.ProposedBidOrPrice} on {detail.CardName}?", PendingActionKind.PlaceMarketBid, detail.ReferenceId, state.Market.ProposedBidOrPrice),
            MarketTab.CreateListing => Confirm("Create Listing?", $"List {detail.CardName} for minimum bid {state.Market.ProposedBidOrPrice}?", PendingActionKind.CreateMarketListing, detail.ReferenceId, state.Market.ProposedBidOrPrice, state.Market.ProposedDurationWeeks),
            _ => new ScreenControllerResult { Message = new MessageBarState(MessageSeverity.Info, "Browse your listing details here. Active changes happen in Browse or Create Listing.") },
        };
    }

    private static ScreenControllerResult Confirm(string title, string line, PendingActionKind kind, string referenceId, int amount, int duration = 1)
    {
        return new ScreenControllerResult
        {
            Modal = new ModalState
            {
                Kind = ModalKind.Confirmation,
                Title = title,
                Lines = [line],
                PendingAction = new PendingAction(kind, title, line, ReferenceId: referenceId, NumericValue: amount, SecondaryNumericValue: duration),
            },
        };
    }

    private static MarketTab Cycle(MarketTab current, int delta)
    {
        return ScreenSelection.Cycle(current, Tabs, delta);
    }
}
