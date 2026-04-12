using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.Deck;

public sealed class DeckState
{
    public int SelectedIndex { get; set; }
    public DeckViewModel? ViewModel { get; set; }
}
