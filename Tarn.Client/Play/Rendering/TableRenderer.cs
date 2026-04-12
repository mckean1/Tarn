using System.Text;

namespace Tarn.ClientApp.Play.Rendering;

public sealed class TableColumn
{
    public required string Header { get; init; }
    public required int Width { get; init; }
    public TableCellAlignment Alignment { get; init; }
}

public enum TableCellAlignment
{
    Left,
    Right,
}

public static class TableRenderer
{
    public static string Render(
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        int selectedRowIndex = -1,
        string columnSeparator = "|",
        string dividerSeparator = "+")
    {
        var builder = new StringBuilder();
        builder.Append(BuildHeader(columns, columnSeparator));
        builder.AppendLine();
        builder.AppendLine(BuildDivider(columns, dividerSeparator));

        for (var index = 0; index < rows.Count; index++)
        {
            var rowText = BuildRow(rows[index], columns, columnSeparator);
            builder.Append(index == selectedRowIndex ? TerminalStyle.Selected(rowText) : rowText);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildHeader(IReadOnlyList<TableColumn> columns, string columnSeparator = "|")
        => BuildRow(columns.Select(column => column.Header).ToList(), columns, columnSeparator);

    public static string BuildDivider(IReadOnlyList<TableColumn> columns, string dividerSeparator = "+", char fill = '-')
        => string.Join(dividerSeparator, columns.Select(column => new string(fill, column.Width)));

    public static string BuildRow(IReadOnlyList<string> values, IReadOnlyList<TableColumn> columns, string columnSeparator = "|")
    {
        var builder = new StringBuilder();
        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(columnSeparator);
            }

            var value = index < values.Count ? values[index] : string.Empty;
            builder.Append(BuildCell(value, columns[index]));
        }

        return builder.ToString();
    }

    private static string BuildCell(string value, TableColumn column)
    {
        var clipped = TextLayout.ClipVisible(value, column.Width);
        return column.Alignment == TableCellAlignment.Right
            ? TextLayout.PadVisibleLeft(clipped, column.Width)
            : TextLayout.PadVisibleRight(clipped, column.Width);
    }
}
