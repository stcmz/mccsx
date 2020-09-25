﻿using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using mccsx.Extensions;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mccsx.Helpers
{
    public static class ExcelHelper
    {
        public static void WriteXlsxFile(
            string filepath,
            string sheetName,
            IEnumerable<IEnumerable<object?>> rows,
            params IEnumerable<object?>[] headerSets)
        {
            // Create a spreadsheet document by supplying the filepath.
            // By default, AutoSave = true, Editable = true, and Type = xlsx.
            var spreadsheetDocument = SpreadsheetDocument.Create(filepath, SpreadsheetDocumentType.Workbook);

            // Add a WorkbookPart to the document.
            var workbookpart = spreadsheetDocument.AddWorkbookPart();
            workbookpart.Workbook = new Workbook();

            // Add a WorksheetPart to the WorkbookPart.
            workbookpart.InsertWorksheet(sheetName, rows, headerSets);

            // Close the document.
            spreadsheetDocument.Close();
        }

        public static void AppendWorksheetToXlsxFile(
            string filepath,
            string sheetName,
            IEnumerable<IEnumerable<object?>> rows,
            params IEnumerable<object?>[] headerSets)
        {
            if (!File.Exists(filepath))
            {
                WriteXlsxFile(filepath, sheetName, rows, headerSets);
                return;
            }

            // Open the document for editing.
            using var spreadsheetDocument = SpreadsheetDocument.Open(filepath, true);

            // Get the existing WorkbookPart from the document.
            var workbookpart = spreadsheetDocument.WorkbookPart;

            // Add a WorksheetPart to the WorkbookPart.
            workbookpart.InsertWorksheet(sheetName, rows, headerSets);

            // Close the document.
            spreadsheetDocument.Close();
        }

        public static void FreezePanel(
            string filepath,
            int sheetIndex,
            uint columns,
            uint rows)
        {
            using var spreadsheetDocument = SpreadsheetDocument.Open(filepath, true);

            // Get the existing WorkbookPart from the document.
            var workbookPart = spreadsheetDocument.WorkbookPart;

            // Get the WorksheetPart at the specific position.
            var worksheetPart = workbookPart.WorksheetParts.Skip(sheetIndex).FirstOrDefault();
            if (worksheetPart == null)
                return;

            worksheetPart.FreezePanelInWorksheet(columns, rows);

            // Close the document.
            spreadsheetDocument.Close();
        }

        public static void AddColorScales(
            string filepath,
            int sheetIndex,
            uint columnBegin,
            uint columnEnd,
            uint rowBegin,
            uint rowEnd,
            params (double value, string color)[] scales)
        {
            using var spreadsheetDocument = SpreadsheetDocument.Open(filepath, true);

            // Get the existing WorkbookPart from the document.
            var workbookPart = spreadsheetDocument.WorkbookPart;

            // Get the WorksheetPart at the specific position.
            var worksheetPart = workbookPart.WorksheetParts.Skip(sheetIndex).FirstOrDefault();
            if (worksheetPart == null)
                return;

            worksheetPart.AddColorScalesInWorksheet(columnBegin, columnEnd, rowBegin, rowEnd, scales);

            // Close the document.
            spreadsheetDocument.Close();
        }
    }
}
