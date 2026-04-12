using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.Market;

public sealed class MarketState
{
    public MarketTab Tab { get; set; } = MarketTab.Browse;
    public int SelectedIndex { get; set; }
    public int ProposedBidOrPrice { get; set; }
    public int ProposedDurationWeeks { get; set; } = 1;
    public MarketViewModel? ViewModel { get; set; }
}
