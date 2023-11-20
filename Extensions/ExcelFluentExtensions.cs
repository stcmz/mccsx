using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mccsx.Extensions;

public static class ExcelFluentExtensions
{
    public static SpreadsheetDocument OpenXlsxFile(this string filepath, bool forceOverwrite)
    {
        if (forceOverwrite || !File.Exists(filepath))
        {
            // Create a spreadsheet document by supplying the filepath.
            // By default, AutoSave = true, Editable = true, and Type = xlsx.
            SpreadsheetDocument spreadsheetDocument = SpreadsheetDocument.Create(filepath, SpreadsheetDocumentType.Workbook);

            // Add a WorkbookPart to the document.
            WorkbookPart workbookpart = spreadsheetDocument.AddWorkbookPart();
            workbookpart.Workbook = new Workbook();

            return spreadsheetDocument;
        }

        return SpreadsheetDocument.Open(filepath, true);
    }

    public static SpreadsheetDocument AppendWorksheet(
        this SpreadsheetDocument spreadsheetDocument,
        string sheetName,
        IEnumerable<IEnumerable<object?>> rows,
        params IEnumerable<object?>[] headerSets)
    {
        // Get the existing WorkbookPart from the document.
        WorkbookPart? workbookpart = spreadsheetDocument.WorkbookPart;

        // Add a WorksheetPart to the WorkbookPart.
        workbookpart.InsertWorksheet(sheetName, rows, headerSets);
        return spreadsheetDocument;
    }

    public static SpreadsheetDocument FreezePanel(this SpreadsheetDocument spreadsheetDocument, int sheetIndex, uint columns, uint rows)
    {
        // Get the existing WorkbookPart from the document.
        WorkbookPart? workbookPart = spreadsheetDocument.WorkbookPart;

        // Get the WorksheetPart at the specific position.
        WorksheetPart? worksheetPart = workbookPart.WorksheetParts.Skip(sheetIndex).FirstOrDefault();
        if (worksheetPart == null)
            return spreadsheetDocument;

        worksheetPart.FreezePanelInWorksheet(columns, rows);
        return spreadsheetDocument;
    }

    public static SpreadsheetDocument AddColorScales(this SpreadsheetDocument spreadsheetDocument, int sheetIndex, uint columnBegin, uint columnEnd, uint rowBegin, uint rowEnd, params (double value, string color)[] scales)
    {
        // Get the existing WorkbookPart from the document.
        WorkbookPart? workbookPart = spreadsheetDocument.WorkbookPart;

        // Get the WorksheetPart at the specific position.
        WorksheetPart? worksheetPart = workbookPart.WorksheetParts.Skip(sheetIndex).FirstOrDefault();
        if (worksheetPart == null)
            return spreadsheetDocument;

        worksheetPart.AddColorScalesInWorksheet(columnBegin, columnEnd, rowBegin, rowEnd, scales);
        return spreadsheetDocument;
    }
}
