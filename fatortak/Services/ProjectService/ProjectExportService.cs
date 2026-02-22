using fatortak.Context;
using fatortak.Entities;
using fatortak.Services.ReportService;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace fatortak.Services.ProjectService
{
    public class ProjectExportService : IProjectExportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ProjectExportService> _logger;

        public ProjectExportService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ProjectExportService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<byte[]> ExportProjectToPdfAsync(Guid projectId)
        {
            var project = await GetProjectDataAsync(projectId);
            if (project == null) return null;

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.TenantId == TenantId);

            using (var stream = new MemoryStream())
            {
                var writer = new PdfWriter(stream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                // Assuming English/Arabic support based on metadata or default
                bool isRtl = true; // Projects usually need Arabic support in this context

                // Load Font for Arabic support
                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                var fontProgram = FontProgramFactory.CreateFont(fontPath);
                var font = PdfFontFactory.CreateFont(fontProgram, PdfEncodings.IDENTITY_H);
                document.SetFont(font);

                if (isRtl)
                {
                    document.SetTextAlignment(TextAlignment.RIGHT);
                    document.SetBaseDirection(BaseDirection.RIGHT_TO_LEFT);
                }

                // 1. Header (Company Info & Logo placeholder)
                Table headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 70, 30 })).UseAllAvailableWidth();
                
                Cell companyInfoCell = new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape(company?.Name ?? "شركة") : company?.Name ?? "Company"))
                    .SetFontSize(16).SetBold().SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                companyInfoCell.Add(new Paragraph(isRtl ? ArabicShaper.Shape(company?.Address ?? "") : company?.Address ?? ""))
                    .SetFontSize(10).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                companyInfoCell.Add(new Paragraph(company?.Phone ?? ""))
                    .SetFontSize(10).SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                headerTable.AddCell(companyInfoCell);

                Cell titleCell = new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape("عرض سعر") : "QUOTATION"))
                    .SetFontSize(20).SetBold().SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetBorder(new SolidBorder(1));
                headerTable.AddCell(titleCell);

                document.Add(headerTable);
                document.Add(new Paragraph("\n"));

                // 2. Project / Client Information
                Table infoTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth();
                
                Cell clientCell = new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape("إلى السيد / السادة:") : "To:"))
                    .SetBold().SetFontSize(11).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                clientCell.Add(new Paragraph(isRtl ? ArabicShaper.Shape(project.Customer?.Name ?? "") : project.Customer?.Name ?? ""))
                    .SetFontSize(12).SetPaddingLeft(10);
                infoTable.AddCell(clientCell);

                Cell projectCell = new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape("اسم المشروع:") : "Project Name:"))
                    .SetBold().SetFontSize(11).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                projectCell.Add(new Paragraph(isRtl ? ArabicShaper.Shape(project.Name) : project.Name))
                    .SetFontSize(12).SetPaddingLeft(10);
                infoTable.AddCell(projectCell);

                document.Add(infoTable);
                document.Add(new Paragraph("\n"));

                // 3. Line Items Table
                Table table = new Table(UnitValue.CreatePercentArray(new float[] { 5, 55, 10, 10, 20 })).UseAllAvailableWidth();
                
                // Header
                string[] headers = isRtl ? new[] { "م", "التوصيف", "الكمية", "الوحدة", "الإجمالي" } : new[] { "#", "Description", "Qty", "Unit", "Total" };
                foreach (var h in headers)
                {
                    table.AddHeaderCell(new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape(h) : h))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetBold().SetTextAlignment(TextAlignment.CENTER));
                }

                int index = 1;
                foreach (var line in project.ProjectLines)
                {
                    table.AddCell(new Cell().Add(new Paragraph(index.ToString())).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape(line.Description) : line.Description)));
                    table.AddCell(new Cell().Add(new Paragraph(line.Quantity.ToString("N2"))).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape(line.Unit ?? "") : line.Unit ?? "")).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph(line.LineTotal.ToString("N2"))).SetTextAlignment(TextAlignment.RIGHT));
                    index++;
                }

                document.Add(table);

                // 4. Totals
                Table totalsTable = new Table(UnitValue.CreatePercentArray(new float[] { 80, 20 })).UseAllAvailableWidth();
                
                AddTotalRow(totalsTable, isRtl ? "الإجمالي" : "Sub-Total", project.ContractValue, isRtl);
                if (project.Discount > 0)
                    AddTotalRow(totalsTable, isRtl ? "الخصم" : "Discount", project.Discount, isRtl);
                
                AddTotalRow(totalsTable, isRtl ? "الصافي" : "Net Total", project.ContractValue - project.Discount, isRtl, true);

                document.Add(totalsTable);
                document.Add(new Paragraph("\n"));

                // 5. Terms & Notes
                if (!string.IsNullOrEmpty(project.PaymentTerms) || !string.IsNullOrEmpty(project.Notes))
                {
                    document.Add(new Paragraph(isRtl ? ArabicShaper.Shape("الشروط والملاحظات:") : "Terms & Conditions:")
                        .SetBold().SetUnderline());
                    
                    if (!string.IsNullOrEmpty(project.PaymentTerms))
                    {
                        document.Add(new Paragraph(isRtl ? ArabicShaper.Shape(project.PaymentTerms) : project.PaymentTerms)
                            .SetFontSize(9).SetItalic());
                    }

                    if (!string.IsNullOrEmpty(project.Notes))
                    {
                        document.Add(new Paragraph(isRtl ? ArabicShaper.Shape(project.Notes) : project.Notes)
                            .SetFontSize(9));
                    }
                }

                // 6. Signature
                document.Add(new Paragraph("\n\n"));
                Table footerTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth();
                footerTable.AddCell(new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape("توقيع العميل") : "Customer Signature"))
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER));
                footerTable.AddCell(new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape("ختم الشركة") : "Company Stamp"))
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER));
                document.Add(footerTable);

                document.Close();
                return stream.ToArray();
            }
        }

        private void AddTotalRow(Table table, string label, decimal value, bool isRtl, bool isBold = false)
        {
            Cell labelCell = new Cell().Add(new Paragraph(isRtl ? ArabicShaper.Shape(label) : label))
                .SetTextAlignment(TextAlignment.RIGHT).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            Cell valueCell = new Cell().Add(new Paragraph(value.ToString("N2") + " EGP"))
                .SetTextAlignment(TextAlignment.RIGHT).SetBorder(new SolidBorder(1));

            if (isBold)
            {
                labelCell.SetBold().SetFontSize(12);
                valueCell.SetBold().SetFontSize(12).SetBackgroundColor(ColorConstants.YELLOW);
            }

            table.AddCell(labelCell);
            table.AddCell(valueCell);
        }

        public async Task<byte[]> ExportProjectToExcelAsync(Guid projectId)
        {
            var project = await GetProjectDataAsync(projectId);
            if (project == null) return null;

            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Project Details");
                ws.View.RightToLeft = true;

                int row = 1;
                // Header
                ws.Cells[row, 1, row, 5].Merge = true;
                ws.Cells[row, 1].Value = "تفاصيل عرض السعر / المشروع";
                ws.Cells[row, 1].Style.Font.Size = 16;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                row += 2;

                ws.Cells[row, 1].Value = "اسم المشروع:";
                ws.Cells[row, 2].Value = project.Name;
                row++;
                ws.Cells[row, 1].Value = "العميل:";
                ws.Cells[row, 2].Value = project.Customer?.Name;
                row += 2;

                // Table Header
                string[] headers = { "م", "التوصيف", "الكمية", "الوحدة", "سعر الوحدة", "الإجمالي" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cells[row, i + 1].Value = headers[i];
                    ws.Cells[row, i + 1].Style.Font.Bold = true;
                    ws.Cells[row, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[row, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    ws.Cells[row, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }
                row++;

                int index = 1;
                foreach (var line in project.ProjectLines)
                {
                    ws.Cells[row, 1].Value = index++;
                    ws.Cells[row, 2].Value = line.Description;
                    ws.Cells[row, 3].Value = line.Quantity;
                    ws.Cells[row, 4].Value = line.Unit;
                    ws.Cells[row, 5].Value = line.UnitPrice;
                    ws.Cells[row, 6].Value = line.LineTotal;
                    
                    ws.Cells[row, 3, row, 6].Style.Numberformat.Format = "#,##0.00";
                    ws.Cells[row, 1, row, 6].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;
                }

                row++;
                ws.Cells[row, 5].Value = "الإجمالي قبل الخصم:";
                ws.Cells[row, 6].Value = project.ContractValue;
                ws.Cells[row, 6].Style.Font.Bold = true;
                row++;
                ws.Cells[row, 5].Value = "الخصم:";
                ws.Cells[row, 6].Value = project.Discount;
                row++;
                ws.Cells[row, 5].Value = "الصافي:";
                ws.Cells[row, 6].Value = project.ContractValue - project.Discount;
                ws.Cells[row, 6].Style.Font.Bold = true;
                ws.Cells[row, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Yellow);

                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                return package.GetAsByteArray();
            }
        }

        private async Task<Project> GetProjectDataAsync(Guid projectId)
        {
            return await _context.Projects
                .Include(p => p.Customer)
                .Include(p => p.ProjectLines)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);
        }
    }
}
