namespace Tarn.ClientApp.Play.Rendering;

public static class AnsiUtility
{
    public static string StripAnsi(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (IsAnsiSequenceStart(text, index))
            {
                index = SkipAnsiSequence(text, index) - 1;
                continue;
            }

            builder.Append(text[index]);
        }

        return builder.ToString();
    }

    public static int GetVisibleLength(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var length = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (IsAnsiSequenceStart(text, index))
            {
                index = SkipAnsiSequence(text, index) - 1;
                continue;
            }

            length++;
        }

        return length;
    }

    internal static bool IsAnsiSequenceStart(string value, int index) =>
        value[index] == '\u001b' && index + 1 < value.Length && value[index + 1] == '[';

    internal static int SkipAnsiSequence(string value, int index)
    {
        var position = index + 2;
        while (position < value.Length)
        {
            var current = value[position++];
            if (current is >= '@' and <= '~')
            {
                break;
            }
        }

        return position;
    }
}
