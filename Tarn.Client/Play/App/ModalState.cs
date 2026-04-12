namespace Tarn.ClientApp.Play.App;

public sealed class ModalState
{
    public ModalKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Lines { get; init; } = [];
    public PendingAction? PendingAction { get; init; }
}
