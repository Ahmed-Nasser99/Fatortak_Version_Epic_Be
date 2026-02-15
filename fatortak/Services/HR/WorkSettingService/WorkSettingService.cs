using fatortak.Context;
using fatortak.Dtos.HR.Settings;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Helpers;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.HR.WorkSettingService
{
    public class WorkSettingService : IWorkSettingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<WorkSettingService> _logger;

        public WorkSettingService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<WorkSettingService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<WorkSettingDto>> GetAsync()
        {
            var workSetting = await _context.WorkSettings
                .Include(ws => ws.GeneralVacations)
                .FirstOrDefaultAsync(ws => ws.TenantId == _tenantId);

            if (workSetting == null)
            {
                workSetting = new WorkSetting
                {
                    TenantId = _tenantId,
                    CreatedAt = DateTime.UtcNow,
                    IsRespectGeneralVacation = true,
                    GeneralVacations = GeneralVacationSeeder.GetEgyptianHolidays(DateTime.Now.Year, _tenantId)
                };

                _context.WorkSettings.Add(workSetting);
                await _context.SaveChangesAsync();
            }

            return ServiceResult<WorkSettingDto>.SuccessResult(MapToDto(workSetting));
        }

        public async Task<ServiceResult<WorkSettingDto>> UpdateAsync(Guid id, UpdateWorkSettingDto dto)
        {
            var workSetting = await _context.WorkSettings
                .Include(ws => ws.GeneralVacations)
                .FirstOrDefaultAsync(ws => ws.TenantId == _tenantId && ws.Id == id);

            if (workSetting == null)
                return ServiceResult<WorkSettingDto>.Failure("الإعدادات غير موجودة");

            workSetting.WorkStartTime = dto.WorkStartTime;
            workSetting.WorkEndTime = dto.WorkEndTime;
            workSetting.WeekendDays = dto.WeekendDays;
            workSetting.GracePeriodMinutes = dto.GracePeriodMinutes;
            workSetting.BreakMinutes = dto.BreakMinutes;
            workSetting.IsRespectGeneralVacation = dto.IsRespectGeneralVacation;
            workSetting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult<WorkSettingDto>.SuccessResult(MapToDto(workSetting));
        }

        public async Task<ServiceResult<GeneralVacationDto>> CreateVacationAsync(CreateGeneralVacationDto dto)
        {
            var workSetting = await _context.WorkSettings
                .FirstOrDefaultAsync(ws => ws.TenantId == _tenantId);

            if (workSetting == null)
                return ServiceResult<GeneralVacationDto>.Failure("لم يتم العثور على إعدادات العمل");

            var vacation = new GeneralVacation
            {
                Name = dto.Name,
                Date = dto.Date,
                DaysOfVacation = dto.DaysOfVacation,
                Notes = dto.Notes,
                TenantId = _tenantId,
                WorkSettingId = workSetting.Id
            };

            _context.GeneralVacations.Add(vacation);
            await _context.SaveChangesAsync();

            return ServiceResult<GeneralVacationDto>.SuccessResult(new GeneralVacationDto
            {
                Id = vacation.Id,
                Name = vacation.Name,
                Date = vacation.Date,
                DaysOfVacation = vacation.DaysOfVacation,
                Notes = vacation.Notes
            });
        }

        public async Task<ServiceResult<GeneralVacationDto>> UpdateVacationAsync(Guid id, UpdateGeneralVacationDto dto)
        {
            var vacation = await _context.GeneralVacations
                .FirstOrDefaultAsync(v => v.TenantId == _tenantId && v.Id == id);

            if (vacation == null)
                return ServiceResult<GeneralVacationDto>.Failure("العطلة غير موجودة");

            vacation.Name = dto.Name;
            vacation.Date = dto.Date;
            vacation.DaysOfVacation = dto.DaysOfVacation;
            vacation.Notes = dto.Notes;
            vacation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ServiceResult<GeneralVacationDto>.SuccessResult(new GeneralVacationDto
            {
                Id = vacation.Id,
                Name = vacation.Name,
                Date = vacation.Date,
                DaysOfVacation = vacation.DaysOfVacation,
                Notes = vacation.Notes
            });
        }

        public async Task<ServiceResult<bool>> DeleteVacationAsync(Guid id)
        {
            var vacation = await _context.GeneralVacations
                .FirstOrDefaultAsync(v => v.TenantId == _tenantId && v.Id == id);

            if (vacation == null)
                return ServiceResult<bool>.Failure("العطلة غير موجودة");

            _context.GeneralVacations.Remove(vacation);
            await _context.SaveChangesAsync();

            return ServiceResult<bool>.SuccessResult(true);
        }

        private WorkSettingDto MapToDto(WorkSetting ws) =>
            new WorkSettingDto
            {
                Id = ws.Id,
                WorkStartTime = ws.WorkStartTime,
                WorkEndTime = ws.WorkEndTime,
                WeekendDays = ws.WeekendDays,
                GracePeriodMinutes = ws.GracePeriodMinutes,
                BreakMinutes = ws.BreakMinutes,
                IsRespectGeneralVacation = ws.IsRespectGeneralVacation,
                GeneralVacations = ws.GeneralVacations
                    .Select(v => new GeneralVacationDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Date = v.Date,
                        DaysOfVacation = v.DaysOfVacation,
                        Notes = v.Notes
                    }).ToList()
            };
    }
}
