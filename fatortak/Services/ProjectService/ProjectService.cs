using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos;
using fatortak.Dtos.Project;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Helpers;
using fatortak.Services.AccountingPostingService;
using Microsoft.EntityFrameworkCore;

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
                    Discount = dto.Discount ?? 0,
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

                ProjectStatus oldStatus = project.Status;

                // Status Transition Validation
                if (oldStatus == ProjectStatus.Completed || oldStatus == ProjectStatus.Cancelled)
                    return ServiceResult<ProjectDto>.Failure($"Cannot change status of a {oldStatus.ToString().ToLower()} project.");

                if (status == ProjectStatus.Draft && (oldStatus == ProjectStatus.Active || oldStatus == ProjectStatus.Completed))
                    return ServiceResult<ProjectDto>.Failure("Cannot revert an active or completed project to draft.");

                if (status == ProjectStatus.Completed)
                {
                    // Check if all sales invoices are paid
                    var hasUnpaidInvoices = await _context.Invoices
                        .AnyAsync(i => i.ProjectId == projectId && 
                                     i.TenantId == TenantId && 
                                     (i.InvoiceType == InvoiceTypes.Sell.ToString() || i.InvoiceType.ToLower() == "sales" || i.InvoiceType.ToLower() == "sale") &&
                                     i.Status != InvoiceStatus.Paid.ToString());

                    if (hasUnpaidInvoices)
                        return ServiceResult<ProjectDto>.Failure("Cannot complete project while there are unpaid invoices.");
                }

                project.Status = status;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // If activated, generate invoice only if not already exists
                if (status == ProjectStatus.Active && oldStatus != ProjectStatus.Active)
                {
                    await GenerateProjectInvoiceAsync(project.Id);
                }

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

                ProjectStatus oldStatus = project.Status;

                // Edits are blocked for Completed, Cancelled, and Active projects
                if (oldStatus == ProjectStatus.Completed || oldStatus == ProjectStatus.Cancelled)
                    return ServiceResult<ProjectDto>.Failure($"Cannot edit a {oldStatus.ToString().ToLower()} project.");

                if (oldStatus == ProjectStatus.Active)
                    return ServiceResult<ProjectDto>.Failure("Cannot edit an active project details. Please use the status update if you need to change its state.");

                if (dto.Status == ProjectStatus.Completed)
                {
                    // Check if all sales invoices are paid
                    var hasUnpaidInvoices = await _context.Invoices
                        .AnyAsync(i => i.ProjectId == projectId && 
                                     i.TenantId == TenantId && 
                                     (i.InvoiceType == InvoiceTypes.Sell.ToString() || i.InvoiceType.ToLower() == "sales" || i.InvoiceType.ToLower() == "sale") &&
                                     i.Status != InvoiceStatus.Paid.ToString());

                    if (hasUnpaidInvoices)
                        return ServiceResult<ProjectDto>.Failure("Cannot complete project while there are unpaid invoices.");
                }

                project.Name = dto.Name;
                project.Description = dto.Description;
                project.CustomerId = dto.CustomerId;
                project.Status = dto.Status;
                project.ContractValue = dto.ContractValue;
                project.Discount = dto.Discount ?? 0;
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
                    Discount = command.Discount ?? 0,
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
                    invoiceId = await GenerateProjectInvoiceAsync(project.Id);
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

        private async Task<Guid?> GenerateProjectInvoiceAsync(Guid projectId)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectLines)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

            if (project == null) return null;

            // Check if invoice already exists to prevent duplicate generation
            var existingInvoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.TenantId == TenantId);

            if (existingInvoice != null)
            {
                // If existing invoice is Draft, move it to Posted to trigger accounting
                if (existingInvoice.Status == InvoiceStatus.Draft.ToString())
                {
                    existingInvoice.Status = InvoiceStatus.Posted.ToString();
                    existingInvoice.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // Ensure it's posted to accounting
                await _accountingPostingService.PostInvoiceAsync(existingInvoice.Id);

                _logger.LogInformation("Invoice already exists for project {ProjectId}, ensured it is posted.", projectId);
                return existingInvoice.Id;
            }

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.TenantId == TenantId);
            if (company == null)
            {
                _logger.LogWarning("Company settings not found, cannot generate invoice for project {ProjectId}", projectId);
                return null;
            }

            var lastInvoice = await _context.Invoices
                .Where(i => i.TenantId == TenantId)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync();

            // Generate invoice number
            var prefix = company.InvoicePrefix ?? "INV";
            var nextNumber = 1;
            if (lastInvoice != null && lastInvoice.InvoiceNumber.Contains("-"))
            {
                var parts = lastInvoice.InvoiceNumber.Split('-');
                if (parts.Length > 1 && int.TryParse(parts[1], out int lastNum))
                    nextNumber = lastNum + 1;
            }
            var invoiceNumber = $"{prefix}-{nextNumber:D4}";

            var userIdString = UserHelper.GetUserId();
            var userId = !string.IsNullOrEmpty(userIdString) ? new Guid(userIdString) : Guid.Empty;

            // a) Create Invoice
            var invoice = new Invoice
            {
                TenantId = TenantId,
                InvoiceNumber = invoiceNumber,
                CustomerId = project.CustomerId,
                UserId = userId,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                Currency = company.Currency,
                InvoiceType = InvoiceTypes.Sell.ToString(),
                Notes = project.Notes,
                Terms = project.PaymentTerms,
                ProjectId = project.Id,
                Status = InvoiceStatus.Posted.ToString(),
                Subtotal = project.ContractValue,
                VatAmount = 0,
                TotalDiscount = project.Discount,
                Total = project.ContractValue - project.Discount,
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

            // b) Post to Accounting
            var posted = await _accountingPostingService.PostInvoiceAsync(invoice.Id);
            if (!posted)
                _logger.LogError("Failed to auto-post invoice {InvoiceId} for project {ProjectId} to accounting", invoice.Id, projectId);

            return invoice.Id;
        }

        public async Task<ServiceResult<bool>> DeleteProjectAsync(Guid projectId)
        {
            try
            {
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<bool>.Failure("Project not found");

                // Check for financial dependencies
                var hasInvoices = await _context.Invoices.AnyAsync(i => i.ProjectId == projectId);
                if (hasInvoices)
                    return ServiceResult<bool>.Failure("Cannot delete project with existing invoices. Please delete the invoices first.");

                var hasExpenses = await _context.Expenses.AnyAsync(e => e.ProjectId == projectId);
                if (hasExpenses)
                    return ServiceResult<bool>.Failure("Cannot delete project with linked expenses. Please reassign or delete the expenses first.");

                var hasJournalEntries = await _context.JournalEntryLines.AnyAsync(l => l.JournalEntry.ProjectId == projectId);
                if (hasJournalEntries)
                    return ServiceResult<bool>.Failure("Cannot delete project with linked accounting journal entries.");

                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {ProjectId}", projectId);
                return ServiceResult<bool>.Failure("Failed to delete project due to a database constraint or system error.");
            }
        }

        public async Task CompleteProjectIfInvoicesPaidAsync(Guid projectId)
        {
            try
            {
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null || project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Cancelled)
                    return;

                // Check if ALL sales invoices are Paid
                var hasUnpaidInvoices = await _context.Invoices
                    .AnyAsync(i => i.ProjectId == projectId && 
                                 i.TenantId == TenantId &&
                                 (i.InvoiceType == InvoiceTypes.Sell.ToString() || i.InvoiceType.ToLower() == "sales" || i.InvoiceType.ToLower() == "sale") &&
                                 i.Status != InvoiceStatus.Paid.ToString());

                if (!hasUnpaidInvoices)
                {
                    _logger.LogInformation("Automatically completing project {ProjectId} as all invoices are paid.", projectId);
                    project.Status = ProjectStatus.Completed;
                    project.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking automatic project completion for {ProjectId}", projectId);
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
                Discount = project.Discount,
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
                .Where(i => i.ProjectId == project.Id && i.TenantId == TenantId && (i.InvoiceType == InvoiceTypes.Sell.ToString() || i.InvoiceType.ToLower() == "sales" || i.InvoiceType.ToLower() == "sale"))
                .SumAsync(i => (decimal?)i.Total) ?? 0;

            var purchaseInvoiceExpenses = await _context.Invoices
                .Where(i => i.ProjectId == project.Id && i.TenantId == TenantId && (i.InvoiceType == InvoiceTypes.Buy.ToString() || i.InvoiceType.ToLower() == "purchase"))
                .SumAsync(i => (decimal?)i.Total) ?? 0;

            dto.TotalExpenses = (await _context.Expenses
                .Where(e => e.ProjectId == project.Id && e.TenantId == TenantId)
                .SumAsync(e => (decimal?)e.Total) ?? 0) + purchaseInvoiceExpenses;

            dto.TotalAdvances = await _context.JournalEntryLines
                .Where(l => l.JournalEntry.ProjectId == project.Id &&
                            l.JournalEntry.TenantId == TenantId &&
                            l.JournalEntry.ReferenceType == JournalEntryReferenceType.Manual)
                .SumAsync(l => (decimal?)l.Debit) ?? 0;

            // Revenue Collected (Credits to AR minus Debits from reversals, where ReferenceType is Payment)
            dto.TotalCollected = await _context.JournalEntryLines
                .Where(l => l.JournalEntry.ProjectId == project.Id &&
                            l.JournalEntry.TenantId == TenantId &&
                            l.JournalEntry.ReferenceType == JournalEntryReferenceType.Payment &&
                            (l.Account.AccountCode.StartsWith("1200") || l.Account.AccountCode.StartsWith("1210")))
                .SumAsync(l => (decimal?)l.Credit - (decimal?)l.Debit) ?? 0;

            // Payments Made (Credits to Cash/Bank/Custody/Advances when paying suppliers/expenses)
            // This captures payments for purchase invoices which also have ReferenceType.Payment
            dto.TotalPaid = await _context.JournalEntryLines
                .Where(l => l.JournalEntry.ProjectId == project.Id &&
                            l.JournalEntry.TenantId == TenantId &&
                            l.JournalEntry.ReferenceType == JournalEntryReferenceType.Payment &&
                            (l.Account.AccountCode.StartsWith("1000") || 
                             l.Account.AccountCode.StartsWith("1100") || 
                             l.Account.AccountCode.StartsWith("1010") || 
                             l.Account.AccountCode.StartsWith("1500")) &&
                            l.Credit > 0)
                .SumAsync(l => (decimal?)l.Credit) ?? 0;
                
            // Direct expenses paid are already captured if they hit cash directly
            // but usually we want to distinguish between payments for invoices and direct expenses.
            // For now, TotalPaid is total outbound cash.

            dto.NetProfit = dto.TotalInvoiced - dto.TotalExpenses;

            return dto;
        }
    }
}
