using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.Collector;

public sealed class CollectorState
{
    public CollectorTab Tab { get; set; } = CollectorTab.Singles;
    public int SelectedIndex { get; set; }
    public CollectorViewModel? ViewModel { get; set; }
}
