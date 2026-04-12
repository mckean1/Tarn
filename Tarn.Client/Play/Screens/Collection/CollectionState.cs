using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.Collection;

public sealed class CollectionState
{
    public CollectionFilter Filter { get; set; } = CollectionFilter.All;
    public CollectionSort Sort { get; set; } = CollectionSort.Name;
    public int SelectedIndex { get; set; }
    public CollectionViewModel? ViewModel { get; set; }
}
