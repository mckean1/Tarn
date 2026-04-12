using System.Text;

namespace Tarn.ClientApp.Play.Rendering;

public sealed class TableColumn
{
    public required string Header { get; init; }
    public required int Width { get; init; }
}

public static class TableRenderer
{
    public static string Render(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        AppendRow(builder, columns.Select(column => column.Header).ToList(), columns);
        builder.AppendLine();
        builder.AppendLine(string.Join("+", columns.Select(column => new string('-', column.Width))));

        foreach (var row in rows)
        {
            AppendRow(builder, row, columns);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> values, IReadOnlyList<TableColumn> columns)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0)
            {
                builder.Append("|");
            }

            var value = index < values.Count ? values[index] : string.Empty;
            builder.Append(Layout.Truncate(value, columns[index].Width));
        }
    }
}
