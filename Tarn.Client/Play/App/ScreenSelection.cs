namespace Tarn.ClientApp.Play.App;

public static class ScreenSelection
{
    public static int Move(int currentIndex, int itemCount, int delta)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        return Math.Clamp(currentIndex + delta, 0, itemCount - 1);
    }

    public static T Cycle<T>(T current, IReadOnlyList<T> items, int delta)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("At least one item is required.", nameof(items));
        }

        var index = FindIndex(items, current);
        if (index < 0)
        {
            index = 0;
        }

        return items[NormalizeIndex(index + delta, items.Count)];
    }

    private static int FindIndex<T>(IReadOnlyList<T> items, T current)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < items.Count; index++)
        {
            if (comparer.Equals(items[index], current))
            {
                return index;
            }
        }

        return -1;
    }

    private static int NormalizeIndex(int index, int count)
    {
        var normalized = index % count;
        return normalized < 0 ? normalized + count : normalized;
    }
}
