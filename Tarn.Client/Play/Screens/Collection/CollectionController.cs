using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.Collection;

public sealed class CollectionController : IPlayScreenController
{
    private static readonly CollectionFilter[] Filters = [CollectionFilter.All, CollectionFilter.Units, CollectionFilter.Spells, CollectionFilter.Counters, CollectionFilter.Champions, CollectionFilter.OwnedOnly];
    private static readonly CollectionSort[] Sorts = [CollectionSort.Name, CollectionSort.Type, CollectionSort.Rarity, CollectionSort.OwnedCount];

    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        switch (action)
        {
            case InputAction.MoveUp:
                state.Collection.SelectedIndex = ScreenSelection.Move(state.Collection.SelectedIndex, state.Collection.ViewModel?.Rows.Count ?? 0, -1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveDown:
                state.Collection.SelectedIndex = ScreenSelection.Move(state.Collection.SelectedIndex, state.Collection.ViewModel?.Rows.Count ?? 0, 1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveLeft:
                state.Collection.Filter = CycleFilter(state.Collection.Filter, -1);
                state.Collection.SelectedIndex = 0;
                return new ScreenControllerResult { RequiresRefresh = true, Message = new MessageBarState(MessageSeverity.Info, $"Filter: {state.Collection.Filter}") };
            case InputAction.MoveRight:
                state.Collection.Filter = CycleFilter(state.Collection.Filter, 1);
                state.Collection.SelectedIndex = 0;
                return new ScreenControllerResult { RequiresRefresh = true, Message = new MessageBarState(MessageSeverity.Info, $"Filter: {state.Collection.Filter}") };
            case InputAction.Select:
                state.Collection.Sort = CycleSort(state.Collection.Sort, 1);
                state.Collection.SelectedIndex = 0;
                return new ScreenControllerResult { RequiresRefresh = true, Message = new MessageBarState(MessageSeverity.Info, $"Sort: {state.Collection.Sort}") };
            case InputAction.Back:
                return new ScreenControllerResult { NavigateTo = ScreenId.Dashboard };
            default:
                return ScreenControllerResult.None;
        }
    }

    private static CollectionFilter CycleFilter(CollectionFilter filter, int delta)
    {
        return ScreenSelection.Cycle(filter, Filters, delta);
    }

    private static CollectionSort CycleSort(CollectionSort sort, int delta)
    {
        return ScreenSelection.Cycle(sort, Sorts, delta);
    }
}
