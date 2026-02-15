using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Company;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.CompanyService
{
    public class CompanyService : ICompanyService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CompanyService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _environment;

        public CompanyService(
            ApplicationDbContext context,
            ILogger<CompanyService> logger,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _environment = environment;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<CompanyDto>> CreateCompanyAsync(CompanyCreateDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<CompanyDto>.Failure("Name is required");

                if (await _context.Companies.AnyAsync(c => c.TenantId == TenantId))
                    return ServiceResult<CompanyDto>.Failure("Company already exists for this tenant");

                var company = new Company
                {
                    TenantId = TenantId,
                    Name = dto.Name,
                    Address = dto.Address,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    TaxNumber = dto.TaxNumber,
                    VATNumber = dto.VATNumber,
                    Currency = dto.Currency,
                    DefaultVatRate = dto.DefaultVatRate,
                    InvoicePrefix = dto.InvoicePrefix
                };

                await _context.Companies.AddAsync(company);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<CompanyDto>.SuccessResult(MapToDto(company));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating company");
                return ServiceResult<CompanyDto>.Failure("Failed to create company");
            }
        }

        public async Task<ServiceResult<string>> UploadCompanyLogoAsync(UploadCompanyLogoDto dto)
        {
            try
            {
                // Validate file
                if (dto.file == null || dto.file.Length == 0)
                    return ServiceResult<string>.Failure("No file uploaded");

                if (dto.file.Length > 5 * 1024 * 1024)
                    return ServiceResult<string>.Failure("File size exceeds 5MB limit");

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(dto.file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                    return ServiceResult<string>.Failure("Invalid file type. Only images are allowed");

                // Get company
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == dto.companyId && c.TenantId == TenantId);

                if (company == null)
                    return ServiceResult<string>.Failure("Company not found");

                // Create upload directory if it doesn't exist
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "company-logos");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Generate unique filename
                var uniqueFileName = $"{dto.companyId}_{DateTime.UtcNow:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.file.CopyToAsync(stream);
                }

                // Update company with new logo URL
                var relativePath = $"/uploads/company-logos/{uniqueFileName}";
                company.LogoUrl = relativePath;
                company.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<string>.SuccessResult(relativePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading logo for company with ID: {dto.companyId}");
                return ServiceResult<string>.Failure("Failed to upload company logo");
            }
        }

        public async Task<ServiceResult<bool>> RemoveCompanyLogoAsync(Guid companyId)
        {
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == companyId && c.TenantId == TenantId);

                if (company == null)
                    return ServiceResult<bool>.Failure("Company not found");

                if (string.IsNullOrEmpty(company.LogoUrl))
                    return ServiceResult<bool>.SuccessResult(true); // No logo to remove

                // Delete the physical file
                var filePath = Path.Combine(_environment.WebRootPath, company.LogoUrl.TrimStart('/'));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Update company
                company.LogoUrl = null;
                company.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing logo for company with ID: {companyId}");
                return ServiceResult<bool>.Failure("Failed to remove company logo");
            }
        }

        public async Task<ServiceResult<CompanyDto>> GetCompanyAsync(Guid companyId)
        {
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == companyId && c.TenantId == TenantId);

                if (company == null)
                    return ServiceResult<CompanyDto>.Failure("Company not found");

                return ServiceResult<CompanyDto>.SuccessResult(MapToDto(company));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving company with ID: {companyId}");
                return ServiceResult<CompanyDto>.Failure("Failed to retrieve company");
            }
        }

        public async Task<ServiceResult<CompanyDto>> GetCurrentTenantCompanyAsync()
        {
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.TenantId == TenantId);

                if (company == null)
                    return ServiceResult<CompanyDto>.Failure("Company not found");

                return ServiceResult<CompanyDto>.SuccessResult(MapToDto(company));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current tenant company");
                return ServiceResult<CompanyDto>.Failure("Failed to retrieve company");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<CompanyDto>>> GetCompaniesAsync(
            CompanyFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _context.Companies
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(filter.Name))
                    query = query.Where(c => c.Name.Contains(filter.Name));

                if (!string.IsNullOrWhiteSpace(filter.TaxNumber))
                    query = query.Where(c => c.TaxNumber.Contains(filter.TaxNumber));

                var totalCount = await query.CountAsync();

                var companies = await query
                    .OrderBy(c => c.Name)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var companyDtos = companies.Select(MapToDto).ToList();

                return ServiceResult<PagedResponseDto<CompanyDto>>.SuccessResult(
                    new PagedResponseDto<CompanyDto>
                    {
                        Data = companyDtos,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving companies");
                return ServiceResult<PagedResponseDto<CompanyDto>>.Failure("Failed to retrieve companies");
            }
        }

        public async Task<ServiceResult<CompanyDto>> UpdateCompanyAsync(Guid companyId, CompanyUpdateDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == companyId && c.TenantId == TenantId);

                if (company == null)
                    return ServiceResult<CompanyDto>.Failure("Company not found");

                if (string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<CompanyDto>.Failure("Name is required");

                company.Name = dto.Name;
                company.Address = dto.Address;
                company.Phone = dto.Phone;
                company.Email = dto.Email;
                company.TaxNumber = dto.TaxNumber;
                company.VATNumber = dto.VATNumber;
                company.Currency = dto?.Currency;
                company.DefaultVatRate = dto?.DefaultVatRate ?? 0;
                company.InvoicePrefix = dto?.InvoicePrefix;
                if (dto?.EnableMultipleBranches.HasValue == true)
                {
                    company.EnableMultipleBranches = dto.EnableMultipleBranches.Value;
                }
                company.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<CompanyDto>.SuccessResult(MapToDto(company));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating company with ID: {companyId}");
                return ServiceResult<CompanyDto>.Failure("Failed to update company");
            }
        }


        public async Task<ServiceResult<CompanyDto>> UpdateCompanyInvoiceTemplateAsync(CompanyUpdateInvoiceTemplateDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var company = await _context.Companies
              .FirstOrDefaultAsync(c => c.TenantId == TenantId);

                if (company == null)
                    return ServiceResult<CompanyDto>.Failure("Company not found");
                if (dto.InvoiceType.ToLower() == InvoiceTypes.Sell.ToString().ToLower())
                {
                    company.SaleInvoiceTemplate = dto.Template;
                    company.SaleInvoiceTemplateColor = dto.Color;
                }
                else if (dto.InvoiceType.ToLower() == InvoiceTypes.Buy.ToString().ToLower())
                {
                    company.PurchaseInvoiceTemplate = dto.Template;
                    company.PurchaseInvoiceTemplateColor = dto.Color;
                }
                else
                {
                    return ServiceResult<CompanyDto>.Failure("Invalid invoice type");
                }


                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<CompanyDto>.SuccessResult(MapToDto(company));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating company with");
                return ServiceResult<CompanyDto>.Failure("Failed to update company");
            }
        }

        public async Task<ServiceResult<bool>> DeleteCompanyAsync(Guid companyId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == companyId && c.TenantId == TenantId);

                if (company == null)
                    return ServiceResult<bool>.Failure("Company not found");

                // Delete logo file if exists
                if (!string.IsNullOrEmpty(company.LogoUrl))
                {
                    var filePath = Path.Combine(_environment.WebRootPath, company.LogoUrl.TrimStart('/'));
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }

                _context.Companies.Remove(company);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting company with ID: {companyId}");
                return ServiceResult<bool>.Failure("Failed to delete company");
            }
        }

        private CompanyDto MapToDto(Company company) => new()
        {
            Id = company.Id,
            Name = company.Name,
            Address = company.Address,
            Phone = company.Phone,
            Email = company.Email,
            TaxNumber = company.TaxNumber,
            VATNumber = company.VATNumber,
            LogoUrl = GenerateImageWithFolderName(company.LogoUrl),
            Currency = company.Currency,
            DefaultVatRate = company.DefaultVatRate,
            InvoicePrefix = company.InvoicePrefix,
            SaleInvoiceTemplate = company.SaleInvoiceTemplate,
            SaleInvoiceTemplateColor = company.SaleInvoiceTemplateColor,
            PurchaseInvoiceTemplate = company.PurchaseInvoiceTemplate,
            PurchaseInvoiceTemplateColor = company.PurchaseInvoiceTemplateColor,
            EnableMultipleBranches = company.EnableMultipleBranches,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };

        private string GenerateImageWithFolderName(string? imageName)
        {
            var request = _httpContextAccessor?.HttpContext?.Request;
            return request != null && imageName != null
                ? $"{request.Scheme}://{request.Host.Value}{imageName}"
                : string.Empty;
        }
    }
}