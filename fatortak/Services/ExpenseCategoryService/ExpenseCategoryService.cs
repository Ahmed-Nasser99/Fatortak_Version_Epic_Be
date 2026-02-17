using fatortak.Context;
using fatortak.Dtos;
using fatortak.Entities;
using fatortak.Services.AuthService;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.ExpenseCategoryService
{
    public interface IExpenseCategoryService
    {
        Task<IEnumerable<ExpenseCategoryDto>> GetAllAsync();
        Task<ExpenseCategoryDto?> GetByIdAsync(Guid id);
        Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryDto dto);
        Task<bool> UpdateAsync(Guid id, UpdateExpenseCategoryDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task SeedDefaultCategoriesAsync(Guid tenantId);
    }

    public class ExpenseCategoryService : IExpenseCategoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ExpenseCategoryService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<IEnumerable<ExpenseCategoryDto>> GetAllAsync()
        {
            return await _context.ExpenseCategories
                .Include(c => c.Account)
                .Where(c => c.TenantId == _tenantId)
                .Select(c => new ExpenseCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    AccountId = c.AccountId,
                    AccountName = c.Account.Name,
                    AccountCode = c.Account.AccountCode
                })
                .ToListAsync();
        }

        public async Task<ExpenseCategoryDto?> GetByIdAsync(Guid id)
        {
            var category = await _context.ExpenseCategories
                .Include(c => c.Account)
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantId);

            if (category == null) return null;

            return new ExpenseCategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                AccountId = category.AccountId,
                AccountName = category.Account.Name,
                AccountCode = category.Account.AccountCode
            };
        }

        public async Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryDto dto)
        {
            var category = new ExpenseCategory
            {
                TenantId = _tenantId,
                Name = dto.Name,
                AccountId = dto.AccountId
            };

            _context.ExpenseCategories.Add(category);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(category.Id);
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateExpenseCategoryDto dto)
        {
            var category = await _context.ExpenseCategories
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantId);

            if (category == null) return false;

            if (!string.IsNullOrEmpty(dto.Name)) category.Name = dto.Name;
            if (dto.AccountId.HasValue) category.AccountId = dto.AccountId.Value;

            category.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var category = await _context.ExpenseCategories
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantId);

            if (category == null) return false;

            _context.ExpenseCategories.Remove(category);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task SeedDefaultCategoriesAsync(Guid tenantId)
        {
            var existing = await _context.ExpenseCategories.AnyAsync(c => c.TenantId == tenantId);
            if (existing) return;

            var defaultMappings = new Dictionary<string, string>
            {
                { "Salaries", "5600" },
                { "Rent", "5500" },
                { "Utilities", "5400" },
                { "Raw Materials", "5300" },
                { "Transportation", "5200" },
                { "Office Expenses", "5100" },
                { "General", "5000" }
            };

            var accounts = await _context.Accounts
                .Where(a => a.TenantId == tenantId && defaultMappings.Values.Contains(a.AccountCode))
                .ToListAsync();

            var categories = new List<ExpenseCategory>();
            foreach (var mapping in defaultMappings)
            {
                var account = accounts.FirstOrDefault(a => a.AccountCode == mapping.Value);
                if (account != null)
                {
                    categories.Add(new ExpenseCategory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = mapping.Key,
                        AccountId = account.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            if (categories.Any())
            {
                _context.ExpenseCategories.AddRange(categories);
                await _context.SaveChangesAsync();
            }
        }
    }
}
