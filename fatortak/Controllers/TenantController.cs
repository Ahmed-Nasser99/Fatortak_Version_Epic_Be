using fatortak.Attributes;
using fatortak.Common.Enum;
using fatortak.Dtos.Shared;
using fatortak.Dtos.Tenant;
using fatortak.Entities;
using fatortak.Services.TenantService;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [AuthorizeRole(RoleEnum.SysAdmin)]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly ITenantService _service;
        private readonly ILogger<TenantController> _logger;

        public TenantController(ITenantService service, ILogger<TenantController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/tenant
        [HttpGet]
        public async Task<ActionResult<ServiceResult<List<Tenant>>>> GetAllTenants()
        {
            try
            {
                var result = await _service.GetAllTenantsAsync();
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all tenants");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        // GET: api/tenant/users/{tenantId}
        [HttpGet("users/{tenantId}")]
        public async Task<ActionResult<ServiceResult<IEnumerable<TenantUserDto>>>> GetTenantUsers(Guid tenantId)
        {
            try
            {
                var result = await _service.GetTenantUsersAsync(tenantId);
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant users for tenant {TenantId}", tenantId);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        // GET: api/tenant/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ServiceResult<Tenant>>> GetUserTenant(Guid userId)
        {
            try
            {
                var result = await _service.GetUserTenantsAsync(userId);
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant for user {UserId}", userId);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpPost("{tenantId}/users")]
        public async Task<ActionResult<ServiceResult<bool>>> AddUserToTenant(Guid tenantId, [FromBody] AddUserToTenantDto dto)
        {
            try
            {
                var result = await _service.AddUserToTenantAsync(tenantId, dto);
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to tenant {TenantId}", tenantId);
                return StatusCode(500, "An error occurred while adding user to tenant");
            }
        }

        [HttpPost("delete/{tenantId}")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteTenant(Guid tenantId)
        {
            try
            {
                var result = await _service.DeleteTenantAsync(tenantId);
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tenant {TenantId}", tenantId);
                return StatusCode(500, "An error occurred while deleting the tenant");
            }
        }
        [HttpPost("{tenantId}/deactivate")]
        public async Task<ActionResult<ServiceResult<bool>>> DeactivateTenant(Guid tenantId)
        {
            try
            {
                var result = await _service.DeactivateTenantAsync(tenantId);
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating tenant {TenantId}", tenantId);
                return StatusCode(500, "An error occurred while deactivating the tenant");
            }
        }
        [HttpPost("{tenantId}/activate")]
        public async Task<ActionResult<ServiceResult<bool>>> ActivateTenant(Guid tenantId)
        {
            try
            {
                var result = await _service.ActivateTenantAsync(tenantId);
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating tenant {TenantId}", tenantId);
                return StatusCode(500, "An error occurred while activating the tenant");
            }
        }
    }
}