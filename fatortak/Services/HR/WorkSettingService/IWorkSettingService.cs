using fatortak.Dtos.HR.Settings;
using fatortak.Dtos.Shared;

namespace fatortak.Services.HR.WorkSettingService
{
    public interface IWorkSettingService
    {
        Task<ServiceResult<WorkSettingDto>> GetAsync();
        Task<ServiceResult<WorkSettingDto>> UpdateAsync(Guid id, UpdateWorkSettingDto dto);
        Task<ServiceResult<GeneralVacationDto>> CreateVacationAsync(CreateGeneralVacationDto dto);
        Task<ServiceResult<GeneralVacationDto>> UpdateVacationAsync(Guid id, UpdateGeneralVacationDto dto);
        Task<ServiceResult<bool>> DeleteVacationAsync(Guid id);
    }
}
