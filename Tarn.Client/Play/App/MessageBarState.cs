namespace Tarn.ClientApp.Play.App;

public sealed record MessageBarState(MessageSeverity Severity, string Text, int TurnsToLive = 4);
