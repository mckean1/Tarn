namespace Tarn.ClientApp.Play.Rendering;

public static class TerminalStyle
{
    public const string Reset = "\u001b[0m";
    public const string BrightWhite = "\u001b[97m";
    public const string Dim = "\u001b[2m";

    public static bool SupportsAnsi =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
        && !string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase);

    public static string Selected(string text) => Wrap(text, BrightWhite);

    public static string Secondary(string text) => Wrap(text, Dim);

    public static bool ContainsAnsi(string? text) => text?.Contains("\u001b[", StringComparison.Ordinal) == true;

    private static string Wrap(string text, string code)
    {
        if (string.IsNullOrEmpty(text) || !SupportsAnsi)
        {
            return text;
        }

        return code + text + Reset;
    }
}
