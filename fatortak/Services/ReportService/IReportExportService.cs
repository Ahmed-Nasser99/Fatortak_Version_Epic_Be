using fatortak.Dtos.Report;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace fatortak.Services.ReportService
{
    public interface IReportExportService
    {
        Task<byte[]> ExportToExcelAsync<T>(List<T> data, ReportMetadata metadata);
        Task<byte[]> ExportToPdfAsync<T>(List<T> data, ReportMetadata metadata);
    }
}
