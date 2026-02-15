using fatortak.Context;
using fatortak.Dtos.Customer;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace fatortak.Services.CustomerService
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustomerService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomerService(
            ApplicationDbContext context,
            ILogger<CustomerService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<CustomerDto>> CreateCustomerAsync(CustomerCreateDto dto)
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<CustomerDto>.Failure("Name is required");

                if (!string.IsNullOrEmpty(dto.Email) && !new EmailAddressAttribute().IsValid(dto.Email))
                    return ServiceResult<CustomerDto>.Failure("Invalid email format");

                var customer = new Customer
                {
                    TenantId = TenantId,
                    Name = dto.Name,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    Address = dto.Address,
                    TaxNumber = dto.TaxNumber,
                    VATNumber = dto.VATNumber,
                    PaymentTerms = dto.PaymentTerms,
                    Notes = dto.Notes,
                    IsSupplier = dto.IsSupplier,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Customers.AddAsync(customer);
                await _context.SaveChangesAsync();

                return ServiceResult<CustomerDto>.SuccessResult(MapToDto(customer));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                return ServiceResult<CustomerDto>.Failure("Failed to create customer");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<CustomerDto>>> GetCustomersAsync(
            CustomerFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _context.Customers
                    .Where(c => !c.IsDeleted)
                    .Where(c => c.TenantId == TenantId)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(filter.Name))
                    query = query.Where(c => c.Name.Contains(filter.Name));

                if (!string.IsNullOrWhiteSpace(filter.Email))
                    query = query.Where(c => c.Email.Contains(filter.Email));

                if (filter.IsSupplier.HasValue)
                    query = query.Where(c => c.IsSupplier == filter.IsSupplier);
                
                
                if (filter.IsActive.HasValue)
                    query = query.Where(c => c.IsActive == filter.IsActive);

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Calculate statistics
                var allCustomersForStats = await query.ToListAsync();
                var weekAgo = DateTime.Now.AddDays(-7);

                var stats = new
                {
                    total = allCustomersForStats.Count,
                    active = allCustomersForStats.Count(c => c.IsActive),
                    inactive = allCustomersForStats.Count(c => !c.IsActive),
                    recent = allCustomersForStats.Count(c => c.CreatedAt > weekAgo)
                };

                // Apply pagination
                var customers = await query
                    .OrderBy(c => c.Name)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var customerDtos = customers.Select(MapToDto).ToList();

                return ServiceResult<PagedResponseDto<CustomerDto>>.SuccessResult(
                    new PagedResponseDto<CustomerDto>
                    {
                        Data = customerDtos,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                return ServiceResult<PagedResponseDto<CustomerDto>>.Failure("Failed to retrieve customers");
            }
        }

        public async Task<ServiceResult<CustomerDto>> GetCustomerAsync(Guid customerId)
        {
            try
            {
                var customer = await _context.Customers
                    .Where(c => !c.IsDeleted)
                    .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

                if (customer == null)
                    return ServiceResult<CustomerDto>.Failure("Customer not found");

                return ServiceResult<CustomerDto>.SuccessResult(MapToDto(customer));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer");
                return ServiceResult<CustomerDto>.Failure("Failed to retrieve customer");
            }
        }

        public async Task<ServiceResult<CustomerDto>> UpdateCustomerAsync(Guid customerId, CustomerUpdateDto dto)
        {
            try
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

                if (customer == null)
                    return ServiceResult<CustomerDto>.Failure("Customer not found");

                // Validate
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<CustomerDto>.Failure("Name is required");

                if (!string.IsNullOrEmpty(dto.Email) && !new EmailAddressAttribute().IsValid(dto.Email))
                    return ServiceResult<CustomerDto>.Failure("Invalid email format");

                customer.Name = dto.Name;
                customer.Email = dto.Email;
                customer.Phone = dto.Phone;
                customer.Address = dto.Address;
                customer.TaxNumber = dto.TaxNumber;
                customer.VATNumber = dto.VATNumber;
                customer.PaymentTerms = dto.PaymentTerms;
                customer.Notes = dto.Notes;
                //customer.IsActive = dto.IsActive;
                //customer.IsSupplier = dto.IsSupplier ?? customer.IsSupplier;
                customer.UpdatedAt = DateTime.UtcNow;
                customer.LastEngagementDate = DateTime.UtcNow;


                await _context.SaveChangesAsync();

                return ServiceResult<CustomerDto>.SuccessResult(MapToDto(customer));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer");
                return ServiceResult<CustomerDto>.Failure("Failed to update customer");
            }
        }

        public async Task<ServiceResult<bool>> ToggleActivation(Guid customerId)
        {
            try
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

                if (customer == null)
                    return ServiceResult<bool>.Failure("Customer not found");

                // Soft delete
                customer.IsActive = !customer.IsActive;
                customer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer");
                return ServiceResult<bool>.Failure("Failed to delete customer");
            }
        }


        public async Task<ServiceResult<bool>> DeleteCustomerAsync(Guid customerId)
        {
            try
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);

                if (customer == null)
                    return ServiceResult<bool>.Failure("Customer not found");

                // Soft delete
                customer.IsDeleted = true;
                customer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer");
                return ServiceResult<bool>.Failure("Failed to delete customer");
            }
        }

        private CustomerDto MapToDto(Customer customer)
        {
            return new CustomerDto
            {
                Id = customer.Id,
                Name = customer.Name,
                Email = customer.Email,
                Phone = customer.Phone,
                Address = customer.Address,
                TaxNumber = customer.TaxNumber,
                VATNumber = customer.VATNumber,
                PaymentTerms = customer.PaymentTerms,
                Notes = customer.Notes,
                IsSupplier = customer.IsSupplier,
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt,
            };
        }
    }
}
