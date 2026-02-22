using fatortak.Dtos.Project;

namespace fatortak.Services.ProjectService
{
    public interface IProjectExportService
    {
        Task<byte[]> ExportProjectToPdfAsync(Guid projectId);
        Task<byte[]> ExportProjectToExcelAsync(Guid projectId);
    }
}
