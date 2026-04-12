using Tarn.ClientApp.Play.App;

namespace Tarn.ClientApp.Play.Screens.Deck;

public sealed class DeckController : IPlayScreenController
{
    public ScreenControllerResult Handle(AppState state, InputAction action)
    {
        switch (action)
        {
            case InputAction.MoveUp:
                state.Deck.SelectedIndex = ScreenSelection.Move(state.Deck.SelectedIndex, state.Deck.ViewModel?.Entries.Count ?? 0, -1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.MoveDown:
                state.Deck.SelectedIndex = ScreenSelection.Move(state.Deck.SelectedIndex, state.Deck.ViewModel?.Entries.Count ?? 0, 1);
                return new ScreenControllerResult { RequiresRefresh = true };
            case InputAction.Select:
                return new ScreenControllerResult
                {
                    Modal = new ModalState
                    {
                        Kind = ModalKind.Confirmation,
                        Title = "Auto-build Best Deck?",
                        Lines =
                        [
                            "Build the best legal deck from your collection.",
                            "This will replace the current active deck.",
                            "Press Y to confirm or Esc to cancel.",
                        ],
                        PendingAction = new PendingAction(PendingActionKind.AutoBuildDeck, "Auto-build Deck", "Build the best legal deck."),
                    },
                };
            case InputAction.Back:
                return new ScreenControllerResult { NavigateTo = ScreenId.Dashboard };
            default:
                return ScreenControllerResult.None;
        }
    }
}
