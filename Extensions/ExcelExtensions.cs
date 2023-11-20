using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using mccsx.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mccsx.Extensions;

public static class ExcelExtensions
{
    // Get the SharedStringTablePart. If it does not exist, create a new one.
    public static SharedStringTablePart InsertSharedStringTablePart(this WorkbookPart workbookPart)
    {
        SharedStringTablePart sharedStringPart;
        if (workbookPart.GetPartsOfType<SharedStringTablePart>().Any())
        {
            sharedStringPart = workbookPart.GetPartsOfType<SharedStringTablePart>().First();
        }
        else
        {
            sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
        }
        return sharedStringPart;
    }

    // Given text and a WorkbookPart, creates a SharedStringItem with the specified text 
    // and inserts it into the SharedStringTablePart. If the item already exists, returns its index.
    public static int InsertSharedStringItem(this WorkbookPart workbookPart, string text)
    {
        return workbookPart.InsertSharedStringTablePart().InsertSharedStringItem(text);
    }

    // Given text and a SharedStringTablePart, creates a SharedStringItem with the specified text 
    // and inserts it into the SharedStringTablePart. If the item already exists, returns its index.
    public static int InsertSharedStringItem(this SharedStringTablePart shareStringPart, string text)
    {
        // If the part does not contain a SharedStringTable, create one.
        shareStringPart.SharedStringTable ??= new SharedStringTable();

        int i = 0;

        // Iterate through all the items in the SharedStringTable. If the text already exists, return its index.
        foreach (SharedStringItem item in shareStringPart.SharedStringTable.Elements<SharedStringItem>())
        {
            if (item.InnerText == text)
                return i;

            i++;
        }

        // The text does not exist in the part. Create the SharedStringItem and return its index.
        shareStringPart.SharedStringTable.AppendChild(new SharedStringItem(new Text(text)));
        shareStringPart.SharedStringTable.Save();

        return i;
    }

    // Given a WorkbookPart, inserts a new worksheet.
    public static WorksheetPart InsertWorksheet(this WorkbookPart workbookPart, string sheetName)
    {
        // Add a new worksheet part to the workbook.
        WorksheetPart newWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        newWorksheetPart.Worksheet = new Worksheet(new SheetData());
        newWorksheetPart.Worksheet.Save();

        Sheets? sheets = workbookPart.Workbook.GetFirstChild<Sheets>();
        sheets ??= workbookPart.Workbook.AppendChild(new Sheets());
        string relationshipId = workbookPart.GetIdOfPart(newWorksheetPart);

        // Get a unique ID for the new sheet.
        uint sheetId = 1;
        if (sheets.Elements<Sheet>().Any())
        {
            sheetId = sheets.Elements<Sheet>().Select(s => s.SheetId.Value).Max() + 1;
        }

        // Append the new worksheet and associate it with the workbook.
        Sheet sheet = new() { Id = relationshipId, SheetId = sheetId, Name = sheetName };
        sheets.Append(sheet);
        workbookPart.Workbook.Save();

        return newWorksheetPart;
    }

    private static CellValues GetCellType(object item)
    {
        return item switch
        {
            bool => CellValues.Boolean,
            string => CellValues.String,
            float f => float.IsNaN(f) ? CellValues.Error : CellValues.Number,
            double d => double.IsNaN(d) ? CellValues.Error : CellValues.Number,
            byte or sbyte or short or ushort or int or uint or long or ulong => CellValues.Number,
            DateTime or DateTimeOffset => CellValues.Date,
            _ => throw new NotSupportedException($"The type {item.GetType()} of row item is not supported"),
        };
    }

    public static Row InsertNewRowInSheetData(
        this SheetData sheetData,
        uint rowIndex,
        IEnumerable<object?> dataRow)
    {
        // Insert a new row with the specified row index.
        Row row = new() { RowIndex = rowIndex };
        sheetData.Append(row);

        uint colIndex = 1;
        List<Cell> cells = [];

        foreach (object? item in dataRow)
        {
            if (item != null)
            {
                // Convert to reference in shape A1.
                string cellReference = ExcelColumnHelper.ToCellRef(colIndex, rowIndex);

                // Insert a new cell.
                Cell cell = new()
                {
                    CellReference = cellReference,
                };

                if (item is not string s)
                {
                    CellValues cellType = GetCellType(item);
                    cell.DataType = cellType;
                    cell.CellValue = new CellValue(cellType == CellValues.Error ? "#N/A" : item.ToString());
                }
                else if (s.StartsWith('='))
                {
                    cell.CellFormula = new CellFormula(s[1..]);
                    cell.CellValue = new CellValue("0");
                }
                else if (s.StartsWith("{=") && s.EndsWith('}'))
                {
                    cell.CellFormula = new CellFormula(s[2..^1])
                    {
                        FormulaType = CellFormulaValues.Array,
                        Reference = cellReference,
                    };
                    cell.CellValue = new CellValue("0");
                }
                else
                {
                    cell.CellValue = new CellValue(item.ToString());
                    cell.DataType = GetCellType(item);
                }

                cells.Add(cell);
            }

            colIndex++;
        }
        row.Append(cells);

        return row;
    }

    public static WorksheetPart InsertWorksheet(
        this WorkbookPart workbookPart,
        string sheetName,
        IEnumerable<IEnumerable<object?>> rows,
        params IEnumerable<object?>[] headerSets)
    {
        // Insert a new worksheet.
        WorksheetPart worksheetPart = workbookPart.InsertWorksheet(sheetName);
        Worksheet worksheet = worksheetPart.Worksheet;
        SheetData? sheetData = worksheet.GetFirstChild<SheetData>();

        // Insert headers.
        uint rowIndex = 0;
        foreach (IEnumerable<object?> headers in headerSets)
            sheetData.InsertNewRowInSheetData(++rowIndex, headers);

        // Insert rows.
        foreach (IEnumerable<object?> row in rows)
            sheetData.InsertNewRowInSheetData(++rowIndex, row);

        worksheet.Save();
        workbookPart.Workbook.Save();

        return worksheetPart;
    }

    public static Cell InsertCellInWorksheet(
        this WorksheetPart worksheetPart,
        string columnName,
        uint rowIndex,
        string value,
        CellValues type)
    {
        // Insert cell A1 into the new worksheet.
        Cell cell = worksheetPart.InsertCellInWorksheet(columnName, rowIndex);

        // Set the value of cell A1.
        cell.CellValue = new CellValue(value);
        cell.DataType = new EnumValue<CellValues>(type);

        // Save the new worksheet.
        worksheetPart.Worksheet.Save();

        return cell;
    }

    // Given a column name, a row index, and a WorksheetPart, inserts a cell into the worksheet. 
    // If the cell already exists, returns it. 
    public static Cell InsertCellInWorksheet(
        this WorksheetPart worksheetPart,
        string columnName,
        uint rowIndex)
    {
        Worksheet worksheet = worksheetPart.Worksheet;
        SheetData? sheetData = worksheet.GetFirstChild<SheetData>();
        string cellReference = columnName + rowIndex;

        // If the worksheet does not contain a row with the specified row index, insert one.
        Row row;
        if (sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).Count() != 0)
        {
            row = sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).First();
        }
        else
        {
            row = new Row { RowIndex = rowIndex };
            sheetData.Append(row);
        }

        // If there is not a cell with the specified column name, insert one.  
        if (row.Elements<Cell>().Where(c => c.CellReference.Value == columnName + rowIndex).Count() > 0)
        {
            return row.Elements<Cell>().Where(c => c.CellReference.Value == cellReference).First();
        }
        else
        {
            // Cells must be in sequential order according to CellReference. Determine where to insert the new cell.
            Cell? refCell = null;
            foreach (Cell cell in row.Elements<Cell>())
            {
                if (string.Compare(cell.CellReference.Value, cellReference, true) > 0)
                {
                    refCell = cell;
                    break;
                }
            }

            Cell newCell = new() { CellReference = cellReference };
            row.InsertBefore(newCell, refCell);

            worksheet.Save();
            return newCell;
        }
    }

    public static WorksheetPart FreezePanelInWorksheet(
        this WorksheetPart worksheetPart,
        uint columns,
        uint rows)
    {
        SheetView sheetView = new()
        {
            TabSelected = false,
            WorkbookViewId = 0U
        };

        sheetView.Append(new Pane()
        {
            VerticalSplit = rows,
            HorizontalSplit = columns,
            TopLeftCell = ExcelColumnHelper.ToCellRef(columns + 1, rows + 1),
            ActivePane = PaneValues.BottomRight,
            State = PaneStateValues.Frozen
        });

        Worksheet worksheet = worksheetPart.Worksheet;
        if (worksheet.SheetViews == null)
        {
            // The SheetViews must appear before the SheetData or the sheet will be corrupted.
            worksheet.PrependChild(new SheetViews(sheetView));
        }
        else
        {
            worksheet.SheetViews.Append(sheetView);
        }

        worksheetPart.Worksheet.Save();

        return worksheetPart;
    }

    public static WorksheetPart AddColorScalesInWorksheet(
        this WorksheetPart worksheetPart,
        uint firstColumn,
        uint lastColumn,
        uint firstRow,
        uint lastRow,
        params (double value, string color)[] scales)
    {
        ColorScale colorScale = new();

        foreach ((double value, string _) in scales)
        {
            colorScale.Append(new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Number, Val = value.ToString() });
        }

        foreach ((double _, string color) in scales)
        {
            colorScale.Append(new Color { Rgb = color });
        }

        ConditionalFormatting conditionalFormatting = new(
            new ConditionalFormattingRule(colorScale)
            {
                Type = new EnumValue<ConditionalFormatValues>(ConditionalFormatValues.ColorScale),
                Priority = 1,
            })
        {
            SequenceOfReferences = new ListValue<StringValue>
            {
                InnerText = $"{ExcelColumnHelper.ToCellRef(firstColumn, firstRow)}:{ExcelColumnHelper.ToCellRef(lastColumn, lastRow)}",
            }
        };

        Worksheet worksheet = worksheetPart.Worksheet;
        worksheet.Append(conditionalFormatting);

        worksheetPart.Worksheet.Save();

        return worksheetPart;
    }
}
