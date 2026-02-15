using fatortak.Dtos.Report;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace fatortak.Services.ReportService
{
    public class ReportExportService : IReportExportService
    {
        public ReportExportService()
        {
            // Set EPPlus license context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<byte[]> ExportToExcelAsync<T>(List<T> data, ReportMetadata metadata)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Report");
                bool isRtl = metadata.Language == "ar";

                if (isRtl)
                {
                    worksheet.View.RightToLeft = true;
                }

                int row = 1;
                int colCount = metadata.Columns.Count;

                // 1. Header Section
                // Title
                worksheet.Cells[row, 1, row, colCount].Merge = true;
                worksheet.Cells[row, 1].Value = metadata.Title;
                worksheet.Cells[row, 1].Style.Font.Size = 16;
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                worksheet.Cells[row, 1, row, colCount].Style.Border.BorderAround(ExcelBorderStyle.Medium);
                row++;

                // Report Date
                worksheet.Cells[row, 1, row, colCount].Merge = true;
                worksheet.Cells[row, 1].Value = $"{(isRtl ? "تاريخ التقرير" : "Report Date")}: {metadata.GeneratedAt}";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;

                // Filters
                if (metadata.Filters != null && metadata.Filters.Any())
                {
                    var filterString = string.Join(", ", metadata.Filters.Select(f => $"{f.Key}: {f.Value}"));
                    worksheet.Cells[row, 1, row, colCount].Merge = true;
                    worksheet.Cells[row, 1].Value = $"{(isRtl ? "الفلاتر المستخدمة" : "Filters Used")}: {filterString}";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    row++;
                }
                
                // Total Records
                worksheet.Cells[row, 1, row, colCount].Merge = true;
                worksheet.Cells[row, 1].Value = $"{(isRtl ? "إجمالي السجلات" : "Total Records")}: {data.Count}";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                worksheet.Cells[row, 1, row, colCount].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                
                row += 2; // Add some spacing

                // 2. Data Table Header
                for (int i = 0; i < colCount; i++)
                {
                    var cell = worksheet.Cells[row, i + 1];
                    cell.Value = metadata.Columns[i].Header;
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                row++;

                // 3. Data Rows
                // 3. Data Rows
                foreach (var item in data)
                {
                    for (int i = 0; i < colCount; i++)
                    {
                        var colDef = metadata.Columns[i];
                        var prop = typeof(T).GetProperty(colDef.PropertyName);
                        
                        // Property might be null or not found, just skip or log warning
                        if (prop == null) continue;

                        var value = prop?.GetValue(item);
                        var cell = worksheet.Cells[row, i + 1];

                        if (value != null)
                        {
                            if (!string.IsNullOrEmpty(colDef.Format) && (value is decimal || value is double || value is float || value is int || value is long))
                            {
                                cell.Value = value;
                                cell.Style.Numberformat.Format = colDef.Format == "C" ? "#,##0.00" : colDef.Format;
                            }
                            else if (value is DateTime dateValue)
                            {
                                cell.Value = dateValue;
                                cell.Style.Numberformat.Format = "yyyy-MM-dd HH:mm";
                            }
                            else
                            {
                                cell.Value = value.ToString();
                            }
                        }

                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    }
                    row++;
                }

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                return await Task.FromResult(package.GetAsByteArray());
            }
        }

        public async Task<byte[]> ExportToPdfAsync<T>(List<T> data, ReportMetadata metadata)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new iText.Kernel.Pdf.PdfWriter(stream);
                var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
                var document = new iText.Layout.Document(pdf);
                bool isRtl = metadata.Language == "ar";

                // Load Font for Arabic support
                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                var fontProgram = iText.IO.Font.FontProgramFactory.CreateFont(fontPath);
                var font = iText.Kernel.Font.PdfFontFactory.CreateFont(fontProgram, iText.IO.Font.PdfEncodings.IDENTITY_H);
                document.SetFont(font);

                if (isRtl)
                {
                    document.SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
                    document.SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                }

                // 1. Header Section
                // Title
                var titleText = isRtl ? ArabicShaper.Shape(metadata.Title) : metadata.Title;
                var title = new iText.Layout.Element.Paragraph(titleText)
                    .SetFontSize(18)
                    .SetBold()
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                    .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
                    .SetPadding(10);
                document.Add(title);

                // Metadata Table
                var metaTable = new iText.Layout.Element.Table(1).UseAllAvailableWidth();
                var dateLabel = isRtl ? ArabicShaper.Shape("تاريخ التقرير") : "Report Date";
                metaTable.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph($"{dateLabel}: {metadata.GeneratedAt}")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                
                if (metadata.Filters != null && metadata.Filters.Any())
                {
                    var filterString = string.Join(", ", metadata.Filters.Select(f => $"{f.Key}: {f.Value}"));
                    if (isRtl) filterString = ArabicShaper.Shape(filterString);
                    var filterLabel = isRtl ? ArabicShaper.Shape("الفلاتر المستخدمة") : "Filters Used";
                    metaTable.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph($"{filterLabel}: {filterString}")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                }

                var recordsLabel = isRtl ? ArabicShaper.Shape("إجمالي السجلات") : "Total Records";
                metaTable.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph($"{recordsLabel}: {data.Count}")).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.YELLOW));
                document.Add(metaTable);
                document.Add(new iText.Layout.Element.Paragraph("\n")); // Spacing

                // 2. Data Table
                var table = new iText.Layout.Element.Table(metadata.Columns.Count).UseAllAvailableWidth();

                // Header Row
                foreach (var col in metadata.Columns)
                {
                    var headerText = isRtl ? ArabicShaper.Shape(col.Header) : col.Header;
                    var cell = new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(headerText));
                    cell.SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY);
                    cell.SetBold();
                    cell.SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
                    table.AddHeaderCell(cell);
                }

                // Data Rows
                foreach (var item in data)
                {
                    foreach (var col in metadata.Columns)
                    {
                        var prop = typeof(T).GetProperty(col.PropertyName);
                        var value = prop?.GetValue(item);
                        string cellValue = "";

                        if (value != null)
                        {
                            if (!string.IsNullOrEmpty(col.Format) && (value is decimal || value is double || value is float || value is int || value is long))
                            {
                                cellValue = string.Format("{0:" + col.Format + "}", value);
                            }
                            else if (value is DateTime dateValue)
                            {
                                cellValue = dateValue.ToString("yyyy-MM-dd HH:mm");
                            }
                            else
                            {
                                cellValue = value.ToString();
                            }
                        }

                        if (isRtl) cellValue = ArabicShaper.Shape(cellValue);

                        var cell = new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(cellValue));
                        if (isRtl) cell.SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
                        table.AddCell(cell);
                    }
                }

                document.Add(table);
                document.Close();

                return await Task.FromResult(stream.ToArray());
            }
        }
    }
}
