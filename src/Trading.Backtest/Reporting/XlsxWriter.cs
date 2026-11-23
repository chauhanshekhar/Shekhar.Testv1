using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Trading.Backtest.Reporting;

/// <summary>Cell look in <see cref="XlsxWriter"/>: index into the cellXfs table written to styles.xml.</summary>
public enum XlsxStyle
{
    Normal = 0,
    Header = 1,        // bold white on dark blue
    FooterText = 2,    // bold on light yellow
    FooterNumber = 3,  // bold +0.00 on light yellow
    Win = 4,           // green +0.00
    Loss = 5,          // red -0.00
    Decimal = 6,       // 0.00
    FooterWin = 7,     // bold green +0.00 on light yellow
    FooterLoss = 8,    // bold red -0.00 on light yellow
}

/// <summary>One cell: a string or a number, and how it looks.</summary>
public readonly record struct XlsxCell(object? Value, XlsxStyle Style = XlsxStyle.Normal);

/// <summary>
/// Minimal single-sheet .xlsx writer (Office Open XML built by hand with System.IO.Compression - no package). Supports
/// strings, numbers, the fixed styles above, column widths, a frozen header row and an auto-filter over the data rows.
/// Report output only; never used by the live pipeline.
/// </summary>
public static class XlsxWriter
{
    public static void Write(string path, string sheetName, IReadOnlyList<IReadOnlyList<XlsxCell>> rows,
        IReadOnlyList<double> columnWidths, int dataRowCount)
    {
        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        Add(zip, "[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
        Add(zip, "_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
        Add(zip, "xl/workbook.xml",
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{SecurityElement.Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>""");
        Add(zip, "xl/_rels/workbook.xml.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
        Add(zip, "xl/styles.xml", Styles);
        Add(zip, "xl/worksheets/sheet1.xml", Sheet(rows, columnWidths, dataRowCount));
    }

    private const string Styles =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""" +
        """<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""" +
        """<numFmts count="1"><numFmt numFmtId="164" formatCode="+#,##0.00;-#,##0.00;0.00"/></numFmts>""" +
        """<fonts count="7">""" +
        """<font><sz val="11"/><name val="Calibri"/></font>""" +
        """<font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>""" +
        """<font><sz val="11"/><color rgb="FF006100"/><name val="Calibri"/></font>""" +
        """<font><sz val="11"/><color rgb="FF9C0006"/><name val="Calibri"/></font>""" +
        """<font><b/><sz val="11"/><name val="Calibri"/></font>""" +
        """<font><b/><sz val="11"/><color rgb="FF006100"/><name val="Calibri"/></font>""" +
        """<font><b/><sz val="11"/><color rgb="FF9C0006"/><name val="Calibri"/></font>""" +
        """</fonts>""" +
        """<fills count="4">""" +
        """<fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill>""" +
        """<fill><patternFill patternType="solid"><fgColor rgb="FF1F4E78"/><bgColor indexed="64"/></patternFill></fill>""" +
        """<fill><patternFill patternType="solid"><fgColor rgb="FFFFF2CC"/><bgColor indexed="64"/></patternFill></fill>""" +
        """</fills>""" +
        """<borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border>""" +
        """<border><left/><right/><top style="thin"><color rgb="FF7F7F7F"/></top><bottom/><diagonal/></border></borders>""" +
        """<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>""" +
        """<cellXfs count="9">""" +
        """<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>""" +
        """<xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>""" +
        """<xf numFmtId="0" fontId="4" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>""" +
        """<xf numFmtId="164" fontId="4" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>""" +
        """<xf numFmtId="164" fontId="2" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/>""" +
        """<xf numFmtId="164" fontId="3" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/>""" +
        """<xf numFmtId="2" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>""" +
        """<xf numFmtId="164" fontId="5" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>""" +
        """<xf numFmtId="164" fontId="6" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>""" +
        """</cellXfs>""" +
        """<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>""" +
        """</styleSheet>""";

    private static string Sheet(IReadOnlyList<IReadOnlyList<XlsxCell>> rows, IReadOnlyList<double> widths, int dataRowCount)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""")
          .Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""")
          .Append("""<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");

        if (widths.Count > 0)
        {
            sb.Append("<cols>");
            for (var c = 0; c < widths.Count; c++)
                sb.Append(CultureInfo.InvariantCulture, $"""<col min="{c + 1}" max="{c + 1}" width="{widths[c]:0.##}" customWidth="1"/>""");
            sb.Append("</cols>");
        }

        sb.Append("<sheetData>");
        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"""<row r="{r + 1}">""");
            for (var c = 0; c < rows[r].Count; c++)
            {
                var cell = rows[r][c];
                var reference = Column(c) + (r + 1).ToString(CultureInfo.InvariantCulture);
                var style = (int)cell.Style;
                switch (cell.Value)
                {
                    case null:
                        if (style != 0)
                            sb.Append(CultureInfo.InvariantCulture, $"""<c r="{reference}" s="{style}"/>""");
                        break;
                    case double or decimal or int or long:
                        var number = Convert.ToDouble(cell.Value, CultureInfo.InvariantCulture);
                        sb.Append(CultureInfo.InvariantCulture, $"""<c r="{reference}" s="{style}"><v>{number.ToString("R", CultureInfo.InvariantCulture)}</v></c>""");
                        break;
                    default:
                        var text = SecurityElement.Escape(Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? "");
                        sb.Append(CultureInfo.InvariantCulture, $"""<c r="{reference}" s="{style}" t="inlineStr"><is><t xml:space="preserve">{text}</t></is></c>""");
                        break;
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");

        var lastColumn = rows.Count > 0 ? Column(rows[0].Count - 1) : "A";
        sb.Append(CultureInfo.InvariantCulture, $"""<autoFilter ref="A1:{lastColumn}{Math.Max(1, dataRowCount + 1)}"/>""");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static string Column(int index)
    {
        var name = "";
        for (var i = index + 1; i > 0; i = (i - 1) / 26)
            name = (char)('A' + (i - 1) % 26) + name;
        return name;
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
