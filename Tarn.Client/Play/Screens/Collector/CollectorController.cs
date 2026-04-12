using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.Collector;

public sealed class CollectorController : IPlayScreenController
{
    private static readonly CollectorTab[] Tabs = [CollectorTab.Singles, CollectorTab.Packs, CollectorTab.Sell];

    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        switch (action)
        {
            case InputAction.MoveUp:
                state.Collector.SelectedIndex = ScreenSelection.Move(state.Collector.SelectedIndex, state.Collector.ViewModel?.Rows.Count ?? 0, -1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveDown:
                state.Collector.SelectedIndex = ScreenSelection.Move(state.Collector.SelectedIndex, state.Collector.ViewModel?.Rows.Count ?? 0, 1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveLeft:
                state.Collector.Tab = Cycle(state.Collector.Tab, -1);
                state.Collector.SelectedIndex = 0;
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveRight:
                state.Collector.Tab = Cycle(state.Collector.Tab, 1);
                state.Collector.SelectedIndex = 0;
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
        var detail = state.Collector.ViewModel?.Detail;
        if (detail is null)
        {
            return new ScreenControllerResult { Message = new MessageBarState(MessageSeverity.Info, "Nothing to act on in this tab.") };
        }

        return state.Collector.Tab switch
        {
            CollectorTab.Singles => Confirm("Buy Single?", $"Buy {detail.Name} for {detail.Price}?", PendingActionKind.BuyCollectorSingle, detail.ReferenceId),
            CollectorTab.Packs => Confirm("Open Pack?", $"Open {detail.Name} for {detail.Price}?", PendingActionKind.BuyCollectorPack, detail.ReferenceId),
            _ => Confirm("Sell to Collector?", $"Sell {detail.Name} for {detail.Price}?", PendingActionKind.SellToCollector, detail.ReferenceId),
        };
    }

    private static ScreenControllerResult Confirm(string title, string line, PendingActionKind kind, string referenceId)
    {
        return new ScreenControllerResult
        {
            Modal = new ModalState
            {
                Kind = ModalKind.Confirmation,
                Title = title,
                Lines = [line, "Press Y to confirm or Esc to cancel."],
                PendingAction = new PendingAction(kind, title, line, ReferenceId: referenceId),
            },
        };
    }

    private static CollectorTab Cycle(CollectorTab current, int delta)
    {
        return ScreenSelection.Cycle(current, Tabs, delta);
    }
}
