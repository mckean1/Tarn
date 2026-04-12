using System.Text;

namespace Tarn.ClientApp.Play.Rendering;

public sealed class TableColumn
{
    public required string Header { get; init; }
    public required int Width { get; init; }
}

public static class TableRenderer
{
    public static string Render(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string>> rows, int selectedRowIndex = -1)
    {
        var builder = new StringBuilder();
        builder.Append(BuildRow(columns.Select(column => column.Header).ToList(), columns));
        builder.AppendLine();
        builder.AppendLine(string.Join("+", columns.Select(column => new string('-', column.Width))));

        for (var index = 0; index < rows.Count; index++)
        {
            var rowText = BuildRow(rows[index], columns);
            builder.Append(index == selectedRowIndex ? TerminalStyle.Selected(rowText) : rowText);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildRow(IReadOnlyList<string> values, IReadOnlyList<TableColumn> columns)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0)
            {
                builder.Append("|");
            }

            var value = index < values.Count ? values[index] : string.Empty;
            builder.Append(Layout.Truncate(value, columns[index].Width));
        }

        return builder.ToString();
    }
}
