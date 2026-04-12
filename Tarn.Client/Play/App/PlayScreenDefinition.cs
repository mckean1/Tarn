using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.App;

public sealed record PlayScreenDefinition(
    ScreenId Id,
    string Title,
    string CompactLabel,
    string FullLabel,
    InputAction? ShortcutAction,
    string ControlsText,
    string HelpText,
    IPlayScreenController Controller,
    Func<AppState, Rect, string> Render,
    Action<RefreshService, AppState>? Refresh);
