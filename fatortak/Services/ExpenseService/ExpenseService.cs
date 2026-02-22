using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Expense;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Services.AccountingPostingService;
using fatortak.Services.TransactionService;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.ExpenseService
{
    public class ExpenseService : IExpenseService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ExpenseService> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ITransactionService _transactionService;
        private readonly IAccountingPostingService _accountingPostingService;

        public ExpenseService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ExpenseService> logger,
            IWebHostEnvironment hostingEnvironment,
            ITransactionService transactionService,
            IAccountingPostingService accountingPostingService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _transactionService = transactionService;
            _accountingPostingService = accountingPostingService;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<PagedResponseDto<ExpenseDto>>> GetAllExpensesAsync(ExpenseFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _context.Expenses
                    .Include(e => e.Project)
                    .Include(e => e.Category)
                    .Include(e => e.PaymentAccount)
                    .Where(e => e.TenantId == _tenantId)
                    .OrderByDescending(e => e.Date)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(filter.Notes))
                    query = query.Where(e => e.Notes.Contains(filter.Notes));

                if (filter.BranchId.HasValue)
                    query = query.Where(e => e.BranchId == filter.BranchId.Value);

                if (filter.ProjectId.HasValue)
                    query = query.Where(e => e.ProjectId == filter.ProjectId.Value);

                if (!string.IsNullOrWhiteSpace(filter.Category) && Guid.TryParse(filter.Category, out var catId))
                    query = query.Where(e => e.CategoryId == catId);

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Calculate statistics
                var stats = new
                {
                    totalCount = totalCount,
                    totalAmount = await query.SumAsync(e => e.Total),
                    thisMonthAmount = await query
                        .Where(e => e.Date.Month == DateTime.Now.Month && e.Date.Year == DateTime.Now.Year)
                        .SumAsync(e => e.Total),
                    lastMonthAmount = await query
                        .Where(e => e.Date.Month == DateTime.Now.AddMonths(-1).Month &&
                                   e.Date.Year == DateTime.Now.AddMonths(-1).Year)
                        .SumAsync(e => e.Total),
                    thisYearAmount = await query
                        .Where(e => e.Date.Year == DateTime.Now.Year)
                        .SumAsync(e => e.Total),
                };

                // Apply pagination
                var expenses = await query
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var expenseDtos = expenses.Select(e => MapToDto(e)).ToList();

                return ServiceResult<PagedResponseDto<ExpenseDto>>.SuccessResult(new PagedResponseDto<ExpenseDto>
                {
                    Data = expenseDtos,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount,
                    MetaData = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all expenses");
                return ServiceResult<PagedResponseDto<ExpenseDto>>.Failure("Failed to retrieve expenses");
            }
        }

        public async Task<ServiceResult<ExpenseDto>> GetExpenseByIdAsync(int id)
        {
            try
            {
                var expense = await _context.Expenses
                    .Include(e => e.Project)
                    .Include(e => e.Category)
                    .Include(e => e.PaymentAccount)
                    .FirstOrDefaultAsync(e => e.TenantId == _tenantId && e.Id == id);

                if (expense == null)
                    return ServiceResult<ExpenseDto>.Failure("Expense not found");

                return ServiceResult<ExpenseDto>.SuccessResult(MapToDto(expense));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expense by ID");
                return ServiceResult<ExpenseDto>.Failure("Failed to retrieve expense");
            }
        }

        public async Task<ServiceResult<ExpenseDto>> CreateExpenseAsync(CreateExpenseDto expenseDto)
        {
            try
            {
                // Handle file upload
                string filePath = null;
                string originalFileName = null;
                if (expenseDto.File != null && expenseDto.File.Length > 0)
                {
                    // Validate file size (10MB limit)
                    if (expenseDto.File.Length > 10 * 1024 * 1024)
                        return ServiceResult<ExpenseDto>.Failure("File size cannot exceed 10MB");

                    // Validate file type
                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".xls", ".xlsx" };
                    var fileExtension = Path.GetExtension(expenseDto.File.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                        return ServiceResult<ExpenseDto>.Failure("File type not allowed. Allowed types: PDF, JPG, PNG, GIF, DOC, DOCX, XLS, XLSX");

                    filePath = await SaveFile(expenseDto.File);
                    originalFileName = expenseDto.File.FileName;
                }

                if (expenseDto.ProjectId.HasValue)
                {
                    var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == expenseDto.ProjectId.Value && p.TenantId == _tenantId);
                    if (project == null)
                        return ServiceResult<ExpenseDto>.Failure("Project not found.");

                    if (project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Cancelled)
                        return ServiceResult<ExpenseDto>.Failure($"Cannot record expense for a {project.Status.ToString().ToLower()} project.");
                }

                var expense = new Expenses
                {
                    Date = expenseDto.Date,
                    Total = expenseDto.Total,
                    Notes = expenseDto.Notes,
                    FilePath = filePath,
                    OriginalFileName = originalFileName,
                    TenantId = _tenantId,
                    BranchId = expenseDto.BranchId,
                    ProjectId = expenseDto.ProjectId,
                    CategoryId = expenseDto.CategoryId,
                    PaymentAccountId = expenseDto.PaymentAccountId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();

                // Load category to get its name for the transaction
                if (expense.CategoryId.HasValue)
                {
                    await _context.Entry(expense).Reference(e => e.Category).LoadAsync();
                }

                // Create Transaction Record
                var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirst("uid")?.Value;
                var userId = !string.IsNullOrEmpty(userIdString) ? Guid.Parse(userIdString) : Guid.Empty;

                var financialTransaction = new Transaction
                {
                    TenantId = _tenantId,
                    TransactionDate = expense.Date.ToDateTime(TimeOnly.MinValue),
                    Type = "Expense",
                    Amount = expense.Total,
                    Direction = "Debit",
                    ReferenceId = expense.Id.ToString(),
                    ReferenceType = "Expense",
                    Description = expense.Notes ?? "Expense",
                    CreatedAt = DateTime.UtcNow,
                    PaymentMethod = "Cash",
                    CreatedBy = userId,
                    BranchId = expense.BranchId,
                    ProjectId = expense.ProjectId,
                    Category = expense.Category?.Name
                };

                // Add Attachment URL to Transaction if file exists
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Construct URL or save path. Usually URL for frontend.
                    // But Transaction entity has string? AttachmentUrl
                    // MapToDto logic generates URL from path. 
                    // Let's save the path or generated URL?
                    // Ideally store what's useful. Path is better for backend, URL for frontend.
                    // Storing Path in AttachmentUrl for consistency with how Expenses store it?
                    // Or keep it null in Transaction and rely on ReferenceId -> Expense -> FilePath?
                    // "System must require attachments... for every transaction".
                    // If Transaction is created from Expense, Expense has file.
                    // If Transaction is direct (e.g. Bank Fee), it might need file.
                    // Here we link them. 
                    financialTransaction.AttachmentUrl = filePath;
                }

                var transactionResult = await _transactionService.AddTransactionAsync(financialTransaction);
                if (!transactionResult.Success)
                {
                    _logger.LogError($"Failed to create transaction for expense {expense.Id}: {transactionResult.ErrorMessage}");
                    // Rollback expense creation
                    _context.Expenses.Remove(expense);
                    await _context.SaveChangesAsync();
                    return ServiceResult<ExpenseDto>.Failure($"Failed to create transaction: {transactionResult.ErrorMessage}");
                }

                // Automatically post expense to accounting
                try
                {
                    var posted = await _accountingPostingService.PostExpenseAsync(expense.Id);
                    if (posted)
                    {
                        _logger.LogInformation("Successfully auto-posted expense {ExpenseId} to accounting", expense.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to auto-post expense {ExpenseId} to accounting. Expense created but not posted.", expense.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-posting expense {ExpenseId} to accounting. Expense created but not posted.", expense.Id);
                    // Don't fail expense creation if posting fails - log and continue
                }

                return ServiceResult<ExpenseDto>.SuccessResult(MapToDto(expense));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expense");
                return ServiceResult<ExpenseDto>.Failure("Failed to create expense");
            }
        }

        public async Task<ServiceResult<ExpenseDto>> UpdateExpenseAsync(int id, UpdateExpenseDto expenseDto)
        {
            try
            {
                var expense = await _context.Expenses
                    .FirstOrDefaultAsync(e => e.TenantId == _tenantId && e.Id == id);

                if (expense == null)
                    return ServiceResult<ExpenseDto>.Failure("Expense not found");

                // Handle file removal/upload omitted for brevity in snippet, assume existing logic preserved if not touched
                // Handle file removal if requested
                if (expenseDto.RemoveFile == true)
                {
                    if (!string.IsNullOrEmpty(expense.FilePath))
                    {
                        DeleteFile(expense.FilePath);
                        expense.FilePath = null;
                        expense.OriginalFileName = null;
                    }
                }

                // Handle new file upload
                if (expenseDto.File != null && expenseDto.File.Length > 0)
                {
                    // Validate file size (10MB limit)
                    if (expenseDto.File.Length > 10 * 1024 * 1024)
                        return ServiceResult<ExpenseDto>.Failure("File size cannot exceed 10MB");

                    // Validate file type
                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".xls", ".xlsx" };
                    var fileExtension = Path.GetExtension(expenseDto.File.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                        return ServiceResult<ExpenseDto>.Failure("File type not allowed. Allowed types: PDF, JPG, PNG, GIF, DOC, DOCX, XLS, XLSX");

                    if (!string.IsNullOrEmpty(expense.FilePath))
                    {
                        DeleteFile(expense.FilePath);
                    }

                    expense.FilePath = await SaveFile(expenseDto.File);
                    expense.OriginalFileName = expenseDto.File.FileName;
                }

                if (expenseDto.Date.HasValue) expense.Date = expenseDto.Date.Value;
                if (expenseDto.Total.HasValue) expense.Total = expenseDto.Total.Value;
                if (expenseDto.Notes != null) expense.Notes = expenseDto.Notes;
                if (expenseDto.BranchId.HasValue) expense.BranchId = expenseDto.BranchId.Value;
                if (expenseDto.ProjectId.HasValue)
                {
                    var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == expenseDto.ProjectId.Value && p.TenantId == _tenantId);
                    if (project == null)
                        return ServiceResult<ExpenseDto>.Failure("Project not found.");

                    if (project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Cancelled)
                        return ServiceResult<ExpenseDto>.Failure($"Cannot link expense to a {project.Status.ToString().ToLower()} project.");

                    expense.ProjectId = expenseDto.ProjectId.Value;
                }
                if (expenseDto.CategoryId.HasValue) expense.CategoryId = expenseDto.CategoryId.Value;
                if (expenseDto.PaymentAccountId.HasValue) expense.PaymentAccountId = expenseDto.PaymentAccountId.Value;

                expense.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Load category to get its name for the transaction
                if (expense.CategoryId.HasValue)
                {
                    await _context.Entry(expense).Reference(e => e.Category).LoadAsync();
                }

                // Update Transaction Record
                var transactionUpdate = new Transaction
                {
                    Amount = expense.Total,
                    TransactionDate = expense.Date.ToDateTime(TimeOnly.MinValue),
                    Description = expense.Notes ?? "Expense",
                    ProjectId = expense.ProjectId,
                    Category = expense.Category?.Name
                };
                await _transactionService.UpdateTransactionByReferenceAsync(expense.Id.ToString(), "Expense", transactionUpdate);

                if (expense.ProjectId.HasValue)
                {
                    // We might need to reload project to map name
                    await _context.Entry(expense).Reference(e => e.Project).LoadAsync();
                }

                return ServiceResult<ExpenseDto>.SuccessResult(MapToDto(expense));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expense");
                return ServiceResult<ExpenseDto>.Failure("Failed to update expense");
            }
        }

        public async Task<ServiceResult<bool>> DeleteExpenseAsync(int id)
        {
            try
            {
                var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.TenantId == _tenantId && e.Id == id);
                if (expense == null) return ServiceResult<bool>.Failure("Expense not found");

                // Delete linked transaction
                await _transactionService.DeleteTransactionByReferenceAsync(id.ToString(), "Expense");

                // Delete file if exists
                if (!string.IsNullOrEmpty(expense.FilePath))
                {
                    DeleteFile(expense.FilePath);
                }

                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expense");
                return ServiceResult<bool>.Failure("Failed to delete expense");
            }
        }

        private ExpenseDto MapToDto(Expenses expense)
        {
            string fileUrl = null;
            string fileName = null;

            if (!string.IsNullOrEmpty(expense.FilePath))
            {
                var request = _httpContextAccessor.HttpContext.Request;
                fileUrl = $"{request.Scheme}://{request.Host}/{expense.FilePath.Replace("\\", "/")}";
                fileName = expense.OriginalFileName ?? Path.GetFileName(expense.FilePath);
            }

            return new ExpenseDto
            {
                Id = expense.Id,
                Date = expense.Date,
                Total = expense.Total,
                Notes = expense.Notes,
                FileUrl = fileUrl,
                FileName = fileName,
                BranchId = expense.BranchId,
                CreatedAt = expense.CreatedAt,
                UpdatedAt = expense.UpdatedAt,
                ProjectId = expense.ProjectId,
                ProjectName = expense.Project?.Name,
                CategoryId = expense.CategoryId,
                CategoryName = expense.Category?.Name,
                PaymentAccountId = expense.PaymentAccountId,
                PaymentAccountName = expense.PaymentAccount?.Name
            };
        }

        private async Task<string> SaveFile(IFormFile file)
        {
            // Ensure the uploads directory exists
            var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", "expenses");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return Path.Combine("uploads", "expenses", uniqueFileName);
        }

        private void DeleteFile(string filePath)
        {
            var fullPath = Path.Combine(_hostingEnvironment.WebRootPath, filePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}