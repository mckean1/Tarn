namespace Tarn.ClientApp.Play.App;

public sealed record PendingAction(
    PendingActionKind Kind,
    string Title,
    string Description,
    string? ReferenceId = null,
    string? SecondaryReferenceId = null,
    int NumericValue = 0,
    int SecondaryNumericValue = 0);
