using ClosedXML.Excel;
using StudyApp.Application.Embeddings;

namespace StudyApp.Infrastructure.Embeddings.Readers;

public class SpreadsheetTextExtractor
{
    public ExtractionResult Extract(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var blocks = new List<ExtractedBlock>();
        var order = 0;

        foreach (var sheet in workbook.Worksheets)
        {
            var rows = sheet.RowsUsed().Select(r =>
                string.Join(" | ", r.CellsUsed().Select(c => c.GetFormattedString().Trim())));

            var text = string.Join("\n", rows);
            if (string.IsNullOrWhiteSpace(text)) continue;

            blocks.Add(new ExtractedBlock(BlockKind.Table, text, sheet.Name, $"sheet:{sheet.Name}", order++));
        }

        return new ExtractionResult(blocks, PageCount: workbook.Worksheets.Count);
    }
}