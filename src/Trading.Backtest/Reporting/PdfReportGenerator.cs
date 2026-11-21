
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Trading.Backtest.Reporting;

public class PdfReportGenerator
{
    public void Generate(string csvFilePath, string pdfFilePath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        if (!File.Exists(csvFilePath))
            throw new FileNotFoundException("Backtest CSV file not found.", csvFilePath);

        var lines = File.ReadAllLines(csvFilePath);

        if (lines.Length < 2)
            throw new InvalidOperationException("CSV contains no backtest results.");

        var headers = ParseCsvLine(lines[0]);
        var rows = lines
            .Skip(1)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(ParseCsvLine)
            .ToList();

        Document.Create(document =>
        {
            foreach (var row in rows)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header()
                        .Column(header =>
                        {
                            header.Item()
                                .Text("Trading Backtest Report")
                                .FontSize(20)
                                .Bold();

                            header.Item()
                                .Text($"Generated: {DateTime.Now:dd-MM-yyyy HH:mm:ss}")
                                .FontSize(8);

                            header.Item()
                                .PaddingVertical(5)
                                .LineHorizontal(1);
                        });

                    page.Content().Column(content =>
                    {
                        content.Spacing(8);

                        content.Item()
                            .Text(GetValue(headers, row, "Configuration"))
                            .FontSize(14)
                            .Bold();

                        content.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(1.5f);
                                });

                                for (int i = 0; i < headers.Count; i += 2)
                                {
                                    AddCell(table, headers[i],
                                        i < row.Count ? row[i] : "");

                                    if (i + 1 < headers.Count)
                                    {
                                        AddCell(table, headers[i + 1],
                                            i + 1 < row.Count ? row[i + 1] : "");
                                    }
                                    else
                                    {
                                        AddCell(table, "", "");
                                    }
                                }
                            });
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text("Trading Backtest Report")
                        .FontSize(8);
                });
            }
        }).GeneratePdf(pdfFilePath);
    }

    private static void AddCell(
        TableDescriptor table,
        string label,
        string value)
    {
        table.Cell()
            .Border(0.5f)
            .Padding(4)
            .Background(Colors.Grey.Lighten3)
            .Text(label)
            .Bold();

        table.Cell()
            .Border(0.5f)
            .Padding(4)
            .Text(value);
    }

    private static string GetValue(
        List<string> headers,
        List<string> row,
        string columnName)
    {
        var index = headers.FindIndex(
            x => x.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        return index >= 0 && index < row.Count
            ? row[index]
            : "Backtest Result";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var value = new System.Text.StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (c == ',' && !insideQuotes)
            {
                result.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(c);
            }
        }

        result.Add(value.ToString());

        return result;
    }
}
