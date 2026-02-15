using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Shared;
using fatortak.Dtos.Tenant;
using fatortak.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.TenantService
{
    public class TenantService : ITenantService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TenantService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;


        public TenantService(
            ApplicationDbContext context,
            ILogger<TenantService> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<ServiceResult<Tenant>> CreateTenantAsync(TenantCreateDto dto, Guid ownerId)
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<Tenant>.Failure("Tenant name is required");

                // Check subdomain uniqueness
                if (!string.IsNullOrEmpty(dto.Subdomain))
                {
                    var exists = await _context.Tenants.AnyAsync(t => t.Subdomain == dto.Subdomain);
                    if (exists)
                        return ServiceResult<Tenant>.Failure("Subdomain already taken");
                }

                // Create tenant
                var tenant = new Tenant
                {
                    Name = dto.Name,
                    Subdomain = dto.Subdomain,
                    IsActive = true
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();

                // Create default Main Branch
                var branch = new Branch
                {
                    TenantId = tenant.Id,
                    Name = "Main Branch",
                    IsMain = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(ownerId.ToString());
                if (user == null)
                {
                    return ServiceResult<Tenant>.Failure("User Not Found");
                }

                user.TenantId = tenant.Id;
                user.Role = RoleEnum.Admin.ToString();
                user.IsActive = true;
                await _userManager.UpdateAsync(user);
                await _context.SaveChangesAsync();

                // Create default company for tenant
                _context.Companies.Add(new Company
                {
                    TenantId = tenant.Id,
                    Name = dto.Name,
                    Currency = "USD",
                    DefaultVatRate = 0.2m,
                    InvoicePrefix = "INV-"
                });

                await _context.SaveChangesAsync();

                return ServiceResult<Tenant>.SuccessResult(tenant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tenant");
                return ServiceResult<Tenant>.Failure("Failed to create tenant");
            }
        }

        public async Task<ServiceResult<bool>> AddUserToTenantAsync(Guid tenantId, AddUserToTenantDto dto)
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(dto.Email))
                    return ServiceResult<bool>.Failure("Email is required");

                // Find user by email
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
                if (user == null)
                    return ServiceResult<bool>.Failure("User not found");

                // Check if already in tenant
                var exists = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == user.Id);

                if (exists != null)
                    return ServiceResult<bool>.Failure("User already in tenant");

                // Add user
                exists.TenantId = tenantId;
                exists.Role = dto.Role ?? RoleEnum.Watcher.ToString(); // Default to User role if not specified
                exists.IsActive = true;

                await _userManager.UpdateAsync(exists);
                await _context.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to tenant");
                return ServiceResult<bool>.Failure("Failed to add user to tenant");
            }
        }

        public async Task<ServiceResult<IEnumerable<TenantUserDto>>> GetTenantUsersAsync(Guid tenantId)
        {
            try
            {
                var users = await _context.Users
                          .Where(t => t.TenantId == tenantId)
                          .Select(tu => new TenantUserDto
                          {
                              UserId = tu.Id,
                              Email = tu.Email,
                              FirstName = tu.FirstName,
                              LastName = tu.LastName,
                              Role = tu.Role,
                              JoinedAt = tu.CreatedAt,
                              IsActive = tu.IsActive
                          }).ToListAsync();

                return ServiceResult<IEnumerable<TenantUserDto>>.SuccessResult(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant users");
                return ServiceResult<IEnumerable<TenantUserDto>>.Failure("Failed to get tenant users");
            }
        }
        public async Task<ServiceResult<Tenant>> GetUserTenantsAsync(Guid userId)
        {
            try
            {
                var tenants = await _userManager.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == userId);
                return ServiceResult<Tenant>.SuccessResult(tenants?.Tenant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant users");
                return ServiceResult<Tenant>.Failure("Failed to get tenant users");
            }
        }


        public async Task<ServiceResult<List<Tenant>>> GetAllTenantsAsync()
        {
            try
            {
                var tenants = await _context.Tenants.ToListAsync();
                return ServiceResult<List<Tenant>>.SuccessResult(tenants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant users");
                return ServiceResult<List<Tenant>>.Failure("Failed to get tenant users");
            }
        }
        public async Task<ServiceResult<bool>> DeleteTenantAsync(Guid tenantId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var tenant = await _context.Tenants
                    .Include(t => t.Users)
                    .FirstOrDefaultAsync(t => t.Id == tenantId);

                if (tenant == null)
                    return ServiceResult<bool>.Failure("Tenant not found");

                // Delete all tenant-related data in proper order to avoid foreign key constraints

                // 1. Delete chat messages and sessions
                var chatSessions = await _context.ChatSessions
                    .Where(cs => cs.TenantId == tenantId)
                    .ToListAsync();

                var chatSessionIds = chatSessions.Select(cs => cs.Id).ToList();

                if (chatSessionIds.Any())
                {
                    var chatMessages = await _context.ChatMessages
                        .Where(cm => chatSessionIds.Contains(cm.SessionId))
                        .ToListAsync();

                    _context.ChatMessages.RemoveRange(chatMessages);
                }
                _context.ChatSessions.RemoveRange(chatSessions);

                // 2. Delete notifications
                var notifications = await _context.Notifications
                    .Where(n => n.TenantId == tenantId)
                    .ToListAsync();
                _context.Notifications.RemoveRange(notifications);

                // 3. Delete expenses
                var expenses = await _context.Expenses
                    .Where(e => e.TenantId == tenantId)
                    .ToListAsync();
                _context.Expenses.RemoveRange(expenses);

                // 4. Delete invoice items and invoices
                var invoices = await _context.Invoices
                    .Where(i => i.TenantId == tenantId)
                    .ToListAsync();

                var invoiceIds = invoices.Select(i => i.Id).ToList();

                if (invoiceIds.Any())
                {
                    var invoiceItems = await _context.InvoiceItems
                        .Where(ii => invoiceIds.Contains(ii.InvoiceId))
                        .ToListAsync();
                    _context.InvoiceItems.RemoveRange(invoiceItems);
                }
                _context.Invoices.RemoveRange(invoices);

                // 5. Delete items
                var items = await _context.Items
                    .Where(i => i.TenantId == tenantId)
                    .ToListAsync();
                _context.Items.RemoveRange(items);

                // 6. Delete customers
                var customers = await _context.Customers
                    .Where(c => c.TenantId == tenantId)
                    .ToListAsync();
                _context.Customers.RemoveRange(customers);

                // 7. Delete companies
                var companies = await _context.Companies
                    .Where(c => c.TenantId == tenantId)
                    .ToListAsync();
                _context.Companies.RemoveRange(companies);

                // 8. Delete subscription
                var subscriptions = await _context.Subscriptions
                    .Where(s => s.TenantId == tenantId)
                    .ToListAsync();
                _context.Subscriptions.RemoveRange(subscriptions);

                // 9. Remove users from tenant (set TenantId to null)
                foreach (var user in tenant.Users)
                {
                    user.TenantId = null;
                    user.IsActive = false;
                    await _userManager.UpdateAsync(user);
                }

                // 10. Finally delete the tenant
                _context.Tenants.Remove(tenant);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Tenant {TenantId} and all related data deleted successfully", tenantId);
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting tenant {TenantId}", tenantId);
                return ServiceResult<bool>.Failure("Failed to delete tenant");
            }
        }

        public async Task<ServiceResult<bool>> DeactivateTenantAsync(Guid tenantId)
        {
            try
            {
                var tenant = await _context.Tenants.FindAsync(tenantId);
                if (tenant == null)
                    return ServiceResult<bool>.Failure("Tenant not found");

                tenant.IsActive = false;
                tenant.UpdatedAt = DateTime.UtcNow;

                // Also deactivate all users in this tenant
                var users = await _context.Users
                    .Where(u => u.TenantId == tenantId)
                    .ToListAsync();

                foreach (var user in users)
                {
                    user.IsActive = false;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Tenant {TenantId} deactivated successfully", tenantId);
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating tenant {TenantId}", tenantId);
                return ServiceResult<bool>.Failure("Failed to deactivate tenant");
            }
        }

        public async Task<ServiceResult<bool>> ActivateTenantAsync(Guid tenantId)
        {
            try
            {
                var tenant = await _context.Tenants.FindAsync(tenantId);
                if (tenant == null)
                    return ServiceResult<bool>.Failure("Tenant not found");

                tenant.IsActive = true;
                tenant.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Tenant {TenantId} activated successfully", tenantId);
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating tenant {TenantId}", tenantId);
                return ServiceResult<bool>.Failure("Failed to activate tenant");
            }
        }
    }
}
