namespace Tarn.ClientApp.Play.App;

public sealed class ScreenControllerResult
{
    public bool RequiresSave { get; init; }
    public bool RequiresRefresh { get; init; }
    public MessageBarState? Message { get; init; }
    public ModalState? Modal { get; init; }
    public ScreenId? NavigateTo { get; init; }

    public static ScreenControllerResult None { get; } = new();
}
