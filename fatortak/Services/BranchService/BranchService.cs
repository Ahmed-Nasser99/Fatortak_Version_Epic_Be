using fatortak.Context;
using fatortak.Dtos;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.BranchService
{
    public class BranchService : IBranchService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BranchService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BranchService(
            ApplicationDbContext context,
            ILogger<BranchService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<BranchDto>> CreateBranchAsync(CreateBranchDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<BranchDto>.Failure("Branch name is required");

                // If this is set as main, unset other main branches for this tenant
                if (dto.IsMain)
                {
                    var currentMain = await _context.Branches
                        .FirstOrDefaultAsync(b => b.TenantId == TenantId && b.IsMain);
                    if (currentMain != null)
                    {
                        currentMain.IsMain = false;
                    }
                }

                var branch = new Branch
                {
                    TenantId = TenantId,
                    Name = dto.Name,
                    Address = dto.Address,
                    Phone = dto.Phone,
                    IsMain = dto.IsMain,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Branches.AddAsync(branch);
                await _context.SaveChangesAsync();

                return ServiceResult<BranchDto>.SuccessResult(MapToDto(branch));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating branch");
                return ServiceResult<BranchDto>.Failure("Failed to create branch");
            }
        }

        public async Task<ServiceResult<List<BranchDto>>> GetBranchesAsync()
        {
            try
            {
                var branches = await _context.Branches
                    .Where(b => b.TenantId == TenantId)
                    .OrderByDescending(b => b.IsMain)
                    .ThenBy(b => b.Name)
                    .ToListAsync();

                return ServiceResult<List<BranchDto>>.SuccessResult(branches.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving branches");
                return ServiceResult<List<BranchDto>>.Failure("Failed to retrieve branches");
            }
        }

        public async Task<ServiceResult<BranchDto>> GetBranchAsync(Guid id)
        {
            try
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == TenantId);

                if (branch == null)
                    return ServiceResult<BranchDto>.Failure("Branch not found");

                return ServiceResult<BranchDto>.SuccessResult(MapToDto(branch));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving branch");
                return ServiceResult<BranchDto>.Failure("Failed to retrieve branch");
            }
        }

        public async Task<ServiceResult<BranchDto>> UpdateBranchAsync(Guid id, UpdateBranchDto dto)
        {
            try
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == TenantId);

                if (branch == null)
                    return ServiceResult<BranchDto>.Failure("Branch not found");

                if (dto.Name != null) branch.Name = dto.Name;
                if (dto.Address != null) branch.Address = dto.Address;
                if (dto.Phone != null) branch.Phone = dto.Phone;
                if (dto.IsActive.HasValue) branch.IsActive = dto.IsActive.Value;

                if (dto.IsMain.HasValue && dto.IsMain.Value && !branch.IsMain)
                {
                    var currentMain = await _context.Branches
                        .FirstOrDefaultAsync(b => b.TenantId == TenantId && b.IsMain);
                    if (currentMain != null)
                    {
                        currentMain.IsMain = false;
                    }
                    branch.IsMain = true;
                }

                branch.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ServiceResult<BranchDto>.SuccessResult(MapToDto(branch));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating branch");
                return ServiceResult<BranchDto>.Failure("Failed to update branch");
            }
        }

        public async Task<ServiceResult<bool>> DeleteBranchAsync(Guid id)
        {
            try
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == TenantId);

                if (branch == null)
                    return ServiceResult<bool>.Failure("Branch not found");

                if (branch.IsMain)
                    return ServiceResult<bool>.Failure("Cannot delete the main branch");

                _context.Branches.Remove(branch);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting branch");
                return ServiceResult<bool>.Failure("Failed to delete branch");
            }
        }

        public async Task<ServiceResult<bool>> ToggleActivationAsync(Guid id)
        {
            try
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == TenantId);

                if (branch == null)
                    return ServiceResult<bool>.Failure("Branch not found");

                branch.IsActive = !branch.IsActive;
                branch.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling branch activation");
                return ServiceResult<bool>.Failure("Failed to toggle branch activation");
            }
        }

        public async Task<ServiceResult<BranchDto>> GetMainBranchAsync()
        {
            try
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.TenantId == TenantId && b.IsMain);

                if (branch == null)
                {
                    // Fallback: Check if there's ANY branch and make it main, or create a new one
                    branch = await _context.Branches.FirstOrDefaultAsync(b => b.TenantId == TenantId);
                    if (branch != null)
                    {
                        branch.IsMain = true;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        branch = new Branch
                        {
                            TenantId = TenantId,
                            Name = "Main Branch",
                            IsMain = true,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Branches.Add(branch);
                        await _context.SaveChangesAsync();
                    }
                }

                return ServiceResult<BranchDto>.SuccessResult(MapToDto(branch));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving main branch");
                return ServiceResult<BranchDto>.Failure("Failed to retrieve main branch");
            }
        }

        private BranchDto MapToDto(Branch branch)
        {
            return new BranchDto
            {
                Id = branch.Id,
                Name = branch.Name,
                Address = branch.Address,
                Phone = branch.Phone,
                IsMain = branch.IsMain,
                IsActive = branch.IsActive
            };
        }
    }
}
