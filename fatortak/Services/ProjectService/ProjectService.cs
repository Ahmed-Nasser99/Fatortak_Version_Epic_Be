using fatortak.Context;
using fatortak.Dtos;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Common.Enum;
using Microsoft.EntityFrameworkCore;
using fatortak.Dtos.Project;
using fatortak.Services.AccountingPostingService;
using fatortak.Helpers;

namespace fatortak.Services.ProjectService
{
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ProjectService> _logger;
        private readonly IAccountingPostingService _accountingPostingService;

        public ProjectService(
            ApplicationDbContext context, 
            IHttpContextAccessor httpContextAccessor, 
            ILogger<ProjectService> logger,
            IAccountingPostingService accountingPostingService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _accountingPostingService = accountingPostingService;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<ProjectDto>> CreateProjectAsync(CreateProjectDto dto)
        {
            try
            {
                var project = new Project
                {
                    TenantId = TenantId,
                    Name = dto.Name,
                    Description = dto.Description,
                    CustomerId = dto.CustomerId,
                    Status = dto.Status,
                    ContractValue = dto.ContractValue,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Projects.AddAsync(project);
                await _context.SaveChangesAsync();


                // Explicitly load Customer if needed, or just map what we have
                if (dto.CustomerId.HasValue)
                {
                    project.Customer = await _context.Customers.FindAsync(dto.CustomerId);
                }

                return ServiceResult<ProjectDto>.SuccessResult(MapToDto(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                return ServiceResult<ProjectDto>.Failure("Failed to create project");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<ProjectDto>>> GetProjectsAsync(PaginationDto pagination, string? name = null, Guid? customerId = null)
        {
            try
            {
                var query = _context.Projects
                    .Include(p => p.Customer)
                    .Include(p => p.ProjectLines)
                    .Where(p => p.TenantId == TenantId)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(p => p.Name.Contains(name));

                if (customerId.HasValue)
                    query = query.Where(p => p.CustomerId == customerId);

                var totalCount = await query.CountAsync();

                var projects = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var dtos = new List<ProjectDto>();
                foreach (var project in projects)
                {
                    dtos.Add(await MapToDtoWithFinancialsAsync(project));
                }

                return ServiceResult<PagedResponseDto<ProjectDto>>.SuccessResult(new PagedResponseDto<ProjectDto>
                {
                    Data = dtos,
                    TotalCount = totalCount,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting projects");
                return ServiceResult<PagedResponseDto<ProjectDto>>.Failure("Failed to get projects");
            }
        }

        public async Task<ServiceResult<ProjectDto>> GetProjectAsync(Guid projectId)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Customer)
                    .Include(p => p.ProjectLines)
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<ProjectDto>.Failure("Project not found");

                return ServiceResult<ProjectDto>.SuccessResult(await MapToDtoWithFinancialsAsync(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project");
                return ServiceResult<ProjectDto>.Failure("Failed to get project");
            }
        }

        public async Task<ServiceResult<ProjectDto>> UpdateProjectStatusAsync(Guid projectId, ProjectStatus status)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<ProjectDto>.Failure("Project not found");

                project.Status = status;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<ProjectDto>.SuccessResult(await MapToDtoWithFinancialsAsync(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project status");
                return ServiceResult<ProjectDto>.Failure("Failed to update project status");
            }
        }

        public async Task<ServiceResult<ProjectDto>> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto)
        {
            try
            {
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<ProjectDto>.Failure("Project not found");

                project.Name = dto.Name;
                project.Description = dto.Description;
                project.CustomerId = dto.CustomerId;
                project.Status = dto.Status;
                project.ContractValue = dto.ContractValue;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                 if (dto.CustomerId.HasValue)
                {
                    project.Customer = await _context.Customers.FindAsync(dto.CustomerId);
                }

                return ServiceResult<ProjectDto>.SuccessResult(await MapToDtoWithFinancialsAsync(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project");
                return ServiceResult<ProjectDto>.Failure("Failed to update project");
            }
        }

        public async Task<ServiceResult<ProjectDto>> CreateProjectWithContractAsync(CreateProjectWithContractCommand command)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1) Validate
                if (command.Lines == null || !command.Lines.Any())
                    return ServiceResult<ProjectDto>.Failure("At least one contract line is required");

                // 2) Calculate totals
                decimal contractValue = 0;
                foreach (var line in command.Lines)
                {
                    if (line.Quantity <= 0 || line.UnitPrice <= 0)
                        return ServiceResult<ProjectDto>.Failure("Quantity and Unit Price must be greater than 0");
                    
                    contractValue += Math.Round(line.Quantity * line.UnitPrice, 2);
                }

                var userId = new Guid(UserHelper.GetUserId());

                // 3) Create Project
                var project = new Project
                {
                    TenantId = TenantId,
                    Name = command.ProjectName,
                    CustomerId = command.ClientId,
                    ContractValue = contractValue,
                    PaymentTerms = command.PaymentTerms,
                    Notes = command.Notes,
                    Status = command.ActivateImmediately ? ProjectStatus.Active : ProjectStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Projects.AddAsync(project);
                await _context.SaveChangesAsync();

                // 4) Create ProjectLines
                foreach (var lineDto in command.Lines)
                {
                    var lineTotal = Math.Round(lineDto.Quantity * lineDto.UnitPrice, 2);
                    var projectLine = new ProjectLine
                    {
                        TenantId = TenantId,
                        ProjectId = project.Id,
                        Description = lineDto.Description,
                        Quantity = lineDto.Quantity,
                        Unit = lineDto.Unit,
                        UnitPrice = lineDto.UnitPrice,
                        LineTotal = lineTotal,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.ProjectLines.AddAsync(projectLine);
                }
                await _context.SaveChangesAsync();

                Guid? invoiceId = null;

                // 5) If ActivateImmediately
                if (command.ActivateImmediately)
                {
                    var company = await _context.Companies.FirstOrDefaultAsync(c => c.TenantId == TenantId);
                    if (company == null)
                        return ServiceResult<ProjectDto>.Failure("Company settings not found");

                    var lastInvoice = await _context.Invoices
                        .Where(i => i.TenantId == TenantId)
                        .OrderByDescending(i => i.CreatedAt)
                        .FirstOrDefaultAsync();

                    // Generate invoice number (simple implementation matching InvoiceService)
                    var prefix = company.InvoicePrefix ?? "INV";
                    var nextNumber = 1;
                    if (lastInvoice != null && lastInvoice.InvoiceNumber.Contains("-"))
                    {
                        var parts = lastInvoice.InvoiceNumber.Split('-');
                        if (parts.Length > 1 && int.TryParse(parts[1], out int lastNum))
                            nextNumber = lastNum + 1;
                    }
                    var invoiceNumber = $"{prefix}-{nextNumber:D4}";

                    // a) Create Invoice
                    var invoice = new Invoice
                    {
                        TenantId = TenantId,
                        InvoiceNumber = invoiceNumber,
                        CustomerId = command.ClientId,
                        UserId = userId,
                        IssueDate = DateTime.UtcNow.Date,
                        DueDate = DateTime.UtcNow.Date.AddDays(30), // Default 30 days
                        Currency = company.Currency,
                        InvoiceType = InvoiceTypes.Sell.ToString(),
                        Notes = project.Notes,
                        Terms = project.PaymentTerms,
                        ProjectId = project.Id,
                        Status = InvoiceStatus.Posted.ToString(),
                        Subtotal = project.ContractValue,
                        VatAmount = 0, // Simplified, can be extended
                        Total = project.ContractValue,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Invoices.Add(invoice);
                    await _context.SaveChangesAsync();

                    // Create Invoice lines from Project lines
                    foreach (var pl in project.ProjectLines)
                    {
                        var invoiceItem = new InvoiceItem
                        {
                            TenantId = TenantId,
                            InvoiceId = invoice.Id,
                            Description = pl.Description,
                            Quantity = (int)pl.Quantity,
                            UnitPrice = pl.UnitPrice,
                            VatRate = 0,
                            LineTotal = pl.LineTotal
                        };
                        _context.InvoiceItems.Add(invoiceItem);
                    }
                    await _context.SaveChangesAsync();

                    // b) Create Journal Entry
                    var posted = await _accountingPostingService.PostInvoiceAsync(invoice.Id);
                    if (!posted)
                        throw new Exception("Failed to post invoice to accounting");

                    invoiceId = invoice.Id;
                }

                await transaction.CommitAsync();

                // 6) Return Project DTO
                project.Customer = await _context.Customers.FindAsync(command.ClientId);
                var dto = MapToDto(project);
                dto.InvoiceId = invoiceId;
                
                return ServiceResult<ProjectDto>.SuccessResult(dto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating project with contract");
                return ServiceResult<ProjectDto>.Failure("Failed to create project with contract setup");
            }
        }

        public async Task<ServiceResult<bool>> DeleteProjectAsync(Guid projectId)
        {
            try
            {
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<bool>.Failure("Project not found");

                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project");
                return ServiceResult<bool>.Failure("Failed to delete project");
            }
        }

        private ProjectDto MapToDto(Project project)
        {
            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                CustomerId = project.CustomerId,
                CustomerName = project.Customer?.Name,
                Status = project.Status,
                ContractValue = project.ContractValue,
                PaymentTerms = project.PaymentTerms,
                Notes = project.Notes,
                IsInternal = project.IsInternal,
                CreatedAt = project.CreatedAt,
                ProjectLines = project.ProjectLines?.Select(l => new ProjectLineDto
                {
                    Id = l.Id,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    Unit = l.Unit,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal
                }).ToList() ?? new List<ProjectLineDto>()
            };
        }

        private async Task<ProjectDto> MapToDtoWithFinancialsAsync(Project project)
        {
            var dto = MapToDto(project);

            dto.TotalInvoiced = await _context.Invoices
                .Where(i => i.ProjectId == project.Id && i.TenantId == TenantId)
                .SumAsync(i => (decimal?)i.Total) ?? 0;

            dto.TotalExpenses = await _context.Expenses
                .Where(e => e.ProjectId == project.Id && e.TenantId == TenantId)
                .SumAsync(e => (decimal?)e.Total) ?? 0;

            dto.TotalAdvances = await _context.JournalEntryLines
                .Where(l => l.JournalEntry.ProjectId == project.Id && l.JournalEntry.TenantId == TenantId && l.JournalEntry.ReferenceType == JournalEntryReferenceType.Payment)
                .SumAsync(l => (decimal?)l.Debit) ?? 0;

            dto.NetProfit = dto.TotalInvoiced - dto.TotalExpenses - dto.TotalAdvances;

            return dto;
        }
    }
}
