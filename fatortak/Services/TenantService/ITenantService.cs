using fatortak.Dtos.Shared;
using fatortak.Dtos.Tenant;
using fatortak.Entities;

namespace fatortak.Services.TenantService
{
    public interface ITenantService
    {
        Task<ServiceResult<Tenant>> CreateTenantAsync(TenantCreateDto dto, Guid ownerId);
        Task<ServiceResult<bool>> AddUserToTenantAsync(Guid tenantId, AddUserToTenantDto dto);
        Task<ServiceResult<IEnumerable<TenantUserDto>>> GetTenantUsersAsync(Guid tenantId);
        Task<ServiceResult<Tenant>> GetUserTenantsAsync(Guid userId);
        Task<ServiceResult<List<Tenant>>> GetAllTenantsAsync();
        Task<ServiceResult<bool>> DeleteTenantAsync(Guid tenantId);
        Task<ServiceResult<bool>> DeactivateTenantAsync(Guid tenantId);
        Task<ServiceResult<bool>> ActivateTenantAsync(Guid tenantId);
    }
}
