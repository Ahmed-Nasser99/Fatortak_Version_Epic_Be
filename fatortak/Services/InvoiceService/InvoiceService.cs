using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Company;
using fatortak.Dtos.Invoice;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Helpers;
using fatortak.Services.AccountingPostingService;
using fatortak.Services.AccountingService;
using fatortak.Services.ProjectService;
using fatortak.Services.TransactionService;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace fatortak.Services.InvoiceService
{
    public class InvoiceService : IInvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ITransactionService _transactionService;
        private readonly IAccountingPostingService _accountingPostingService;
        private readonly IAccountingService _accountingService;
        private readonly IProjectService _projectService;

        public InvoiceService(
            ApplicationDbContext context,
            ILogger<InvoiceService> logger,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment hostingEnvironment,
            ITransactionService transactionService,
            IAccountingPostingService accountingPostingService,
            IAccountingService accountingService,
            IProjectService projectService)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _hostingEnvironment = hostingEnvironment;
            _transactionService = transactionService;
            _accountingPostingService = accountingPostingService;
            _accountingService = accountingService;
            _projectService = projectService;
        }

        private Guid TenantId => GetCurrentTenantId();
        #region Create Invoice 
        public async Task<ServiceResult<InvoiceDto>> CreateInvoiceFromOcrAsync(OcrInvoiceCreateDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var userId = new Guid(UserHelper.GetUserId());
                var company = await _context.Companies.FirstOrDefaultAsync(c => c.TenantId == TenantId);
                if (company == null)
                    return ServiceResult<InvoiceDto>.Failure("Company not found");

                Customer customer;

                // Handle customer/supplier creation/retrieval based on invoice type
                if (dto.CustomerId.HasValue)
                {
                    customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId.Value && c.TenantId == TenantId);
                    if (customer == null)
                        return ServiceResult<InvoiceDto>.Failure("Customer not found");
                }
                else
                {
                    // Determine if we're creating a supplier or customer based on invoice type
                    bool isBuyInvoice = dto.InvoiceType?.ToLower() == InvoiceTypes.Buy.ToString().ToLower();

                    string entityName, entityEmail, entityPhone, entityAddress, entityTaxNumber, entityVATNumber;

                    if (isBuyInvoice)
                    {
                        // For Buy invoices, we're buying FROM a supplier (seller info)
                        entityName = dto.SallerName;
                        entityEmail = dto.SallerEmail;
                        entityPhone = dto.SallerPhone;
                        entityAddress = dto.SallerAddress;
                        entityTaxNumber = dto.SallerTaxNumber;
                        entityVATNumber = dto.SallerVATNumber;
                    }
                    else
                    {
                        // For Sell invoices, we're selling TO a customer (buyer info)
                        entityName = dto.BuyerName;
                        entityEmail = dto.BuyerEmail;
                        entityPhone = dto.BuyerPhone;
                        entityAddress = dto.BuyerAddress;
                        entityTaxNumber = dto.BuyerTaxNumber;
                        entityVATNumber = dto.BuyerVATNumber;
                    }

                    if (string.IsNullOrWhiteSpace(entityName))
                    {
                        var entityType = isBuyInvoice ? "Supplier" : "Customer";
                        return ServiceResult<InvoiceDto>.Failure($"{entityType} information is required when CustomerId is not provided");
                    }

                    // Check if customer/supplier already exists by name or email
                    customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.TenantId == TenantId &&
                            (c.Name == entityName ||
                             (!string.IsNullOrWhiteSpace(entityEmail) && c.Email == entityEmail)));

                    if (customer == null)
                    {
                        // Create new customer/supplier from OCR data
                        customer = new Customer
                        {
                            TenantId = TenantId,
                            Name = entityName,
                            Email = entityEmail,
                            Phone = entityPhone,
                            Address = entityAddress,
                            TaxNumber = entityTaxNumber,
                            VATNumber = entityVATNumber,
                            IsSupplier = isBuyInvoice,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync();
                    }
                    else if (isBuyInvoice && !customer.IsSupplier)
                    {
                        customer.IsSupplier = true;
                        customer.UpdatedAt = DateTime.UtcNow;
                    }
                }

                var lastInvoice = await _context.Invoices
                    .Where(i => i.TenantId == TenantId)
                    .OrderByDescending(i => i.CreatedAt)
                    .FirstOrDefaultAsync();

                var invoiceNumber = GenerateInvoiceNumber(company.InvoicePrefix, lastInvoice?.InvoiceNumber);

                // Create invoice - ALWAYS start as DRAFT
                var invoice = new Invoice
                {
                    TenantId = TenantId,
                    InvoiceNumber = invoiceNumber,
                    CustomerId = customer.Id,
                    UserId = userId,
                    IssueDate = dto.IssueDate,
                    DueDate = dto.DueDate,
                    Currency = company.Currency,
                    InvoiceType = dto.InvoiceType,
                    Notes = dto.Notes,
                    Terms = dto.Terms,
                    DownPayment = dto.DownPayment.GetValueOrDefault(),
                    Benefits = dto.Benefits.GetValueOrDefault(),
                    BranchId = dto.BranchId,
                    ProjectId = dto.ProjectId,
                    Status = InvoiceStatus.Draft.ToString() // Always start as draft
                };

                // Process items WITHOUT updating inventory yet
                foreach (var itemDto in dto.Items)
                {
                    Item item = null;

                    if (itemDto.ItemId.HasValue)
                    {
                        item = await _context.Items.FindAsync(itemDto.ItemId.Value);
                    }
                    else if (!string.IsNullOrWhiteSpace(itemDto.Name))
                    {
                        item = await _context.Items
                            .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Name == itemDto.Name);

                        if (item == null)
                        {
                            bool isExpense = dto.PurchaseType?.ToLower() == InvoicePurchaseType.expenses.ToString().ToLower();

                            item = new Item
                            {
                                TenantId = TenantId,
                                Name = itemDto.Name,
                                Description = itemDto.Description ?? itemDto.Name,
                                UnitPrice = itemDto.UnitPrice,
                                VatRate = itemDto.VatRate ?? company.DefaultVatRate,
                                Type = isExpense ? "expenses" : "product",
                                IsDeleted = isExpense,
                                Quantity = 0,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.Items.Add(item);
                            await _context.SaveChangesAsync();
                        }
                    }

                    var unitPrice = itemDto.UnitPrice > 0 ? itemDto.UnitPrice : item?.UnitPrice ?? 0;
                    var vatRate = itemDto.VatRate ?? item?.VatRate ?? company?.DefaultVatRate ?? 0;

                    var description = !string.IsNullOrWhiteSpace(itemDto.Description) ? itemDto.Description :
                                     !string.IsNullOrWhiteSpace(itemDto.Name) ? itemDto.Name :
                                     item?.Name ?? "Unknown Item";

                    var lineTotal = CalculateLineTotal(
                        itemDto.Quantity,
                        unitPrice,
                        itemDto.Discount,
                        vatRate);

                    var invoiceItem = new InvoiceItem
                    {
                        TenantId = TenantId,
                        Description = description,
                        Quantity = itemDto.Quantity,
                        UnitPrice = unitPrice,
                        VatRate = vatRate,
                        Discount = itemDto.Discount,
                        LineTotal = lineTotal,
                        ItemId = item?.Id
                    };

                    invoice.InvoiceItems.Add(invoiceItem);
                }

                // Calculate totals
                var (subtotal, vatAmount, totalDiscount, total) = CalculateInvoiceTotals(invoice.InvoiceItems.ToList());
                invoice.Subtotal = subtotal;
                invoice.VatAmount = vatAmount;
                invoice.TotalDiscount = totalDiscount;
                invoice.Total = total + dto.Benefits.GetValueOrDefault();

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                // Handle final status and inventory updates ONLY if not staying in Draft
                if (dto.Status.ToLower() == InvoiceStatus.Paid.ToString().ToLower() || dto.Status.ToLower() == InvoiceStatus.PartialPaid.ToString().ToLower())
                {
                    await UpdateInventoryForInvoiceAsync(invoice, dto.InvoiceType);

                    // Update customer engagement date for paid statuses
                    if (dto.Status == InvoiceStatus.Paid.ToString() || dto.Status == InvoiceStatus.PartialPaid.ToString())
                    {
                        invoice.Customer.LastEngagementDate = DateTime.UtcNow;
                    }

                    // Set final status and handle payments/installments
                    invoice.Status = dto.Status;

                    if (dto.Status == InvoiceStatus.Paid.ToString())
                    {
                        invoice.AmountPaid = invoice.Total;
                        invoice.PaidAt = DateTime.UtcNow;

                        await RegisterPaymentAsync(invoice, invoice.Total, "Cash", null, dto.PaymentAccountId);
                    }
                    else if (dto.NumberOfInstallments > 0)
                    {
                        invoice.Status = InvoiceStatus.Pending.ToString();
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var createdInvoice = await GetInvoiceWithRelations(invoice.Id);
                return ServiceResult<InvoiceDto>.SuccessResult(MapToDto(createdInvoice));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating invoice from OCR data");
                return ServiceResult<InvoiceDto>.Failure("Failed to create invoice from OCR data");
            }
        }

        public async Task<ServiceResult<InvoiceDto>> CreateInvoiceAsync(InvoiceCreateDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var userId = new Guid(UserHelper.GetUserId());
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId && c.TenantId == TenantId);
                if (customer == null)
                    return ServiceResult<InvoiceDto>.Failure("Customer not found");

                var company = await _context.Companies.FirstOrDefaultAsync(c => c.TenantId == TenantId);
                if (company == null)
                    return ServiceResult<InvoiceDto>.Failure("Company not found");

                var lastInvoice = await _context.Invoices
                    .Where(i => i.TenantId == TenantId)
                    .OrderByDescending(i => i.CreatedAt)
                    .FirstOrDefaultAsync();

                var invoiceNumber = !string.IsNullOrEmpty(dto.InvoiceNumber)
                    ? dto.InvoiceNumber
                    : GenerateInvoiceNumber(company.InvoicePrefix, lastInvoice?.InvoiceNumber);

                if (dto.ProjectId.HasValue)
                {
                    var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId && p.TenantId == TenantId);
                    if (project == null)
                        return ServiceResult<InvoiceDto>.Failure("Project not found.");

                    if (project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Cancelled)
                        return ServiceResult<InvoiceDto>.Failure($"Cannot create invoice for a {project.Status.ToString().ToLower()} project.");

                    // Sales invoice restriction: Only one per project
                    if (dto.InvoiceType?.ToLower() == InvoiceTypes.Sell.ToString().ToLower())
                    {
                        var existingSalesInvoice = await _context.Invoices
                            .AnyAsync(i => i.ProjectId == dto.ProjectId &&
                                           i.TenantId == TenantId &&
                                           (i.InvoiceType.ToLower() == InvoiceTypes.Sell.ToString().ToLower() ||
                                            i.InvoiceType.ToLower() == "sales" ||
                                            i.InvoiceType.ToLower() == "sale"));

                        if (existingSalesInvoice)
                            return ServiceResult<InvoiceDto>.Failure("A sales invoice already exists for this project. Each project can only have one sales invoice.");
                    }
                }

                if (dto.Items == null || !dto.Items.Any())
                {
                    dto.Items = ParseJsonFromForm<InvoiceItemCreateDto>("items");
                }

                if ((dto.Installments == null || !dto.Installments.Any()) && dto.NumberOfInstallments > 0)
                {
                    dto.Installments = ParseJsonFromForm<InstallmentCreateDto>("installments");
                }


                // Create invoice - ALWAYS start as DRAFT
                var invoice = new Invoice
                {
                    TenantId = TenantId,
                    InvoiceNumber = invoiceNumber,
                    CustomerId = customer.Id,
                    UserId = userId,
                    IssueDate = dto.IssueDate,
                    DueDate = dto.DueDate,
                    Currency = company.Currency,
                    InvoiceType = dto.InvoiceType,
                    Notes = dto.Notes,
                    Terms = dto.Terms,
                    DownPayment = dto.DownPayment.GetValueOrDefault(),
                    Benefits = dto.Benefits.GetValueOrDefault(),
                    BranchId = dto.BranchId,
                    ProjectId = dto.ProjectId,
                    Status = dto.Status ?? InvoiceStatus.Draft.ToString(), // Always start as draft
                    AttachmentUrl = dto.AttachmentUrl
                };

                // Process items WITHOUT updating inventory yet
                foreach (var itemDto in dto.Items)
                {
                    var item = itemDto.ItemId != null
                        ? await _context.Items.FindAsync(itemDto.ItemId)
                        : null;

                    var unitPrice = itemDto.UnitPrice > 0 ? itemDto.UnitPrice : item?.UnitPrice ?? 0;
                    var vatRate = (itemDto.VatRate.HasValue && itemDto.VatRate.Value > 0)
                                ? itemDto.VatRate.Value
                                : item?.VatRate ?? company?.DefaultVatRate ?? 0;

                    var lineTotal = CalculateLineTotal(
                        itemDto.Quantity,
                        unitPrice,
                        itemDto.Discount,
                        vatRate);

                    var invoiceItem = new InvoiceItem
                    {
                        TenantId = TenantId,
                        Description = itemDto.Description ?? item?.Name ?? "Unknown Item",
                        Quantity = itemDto.Quantity,
                        UnitPrice = unitPrice,
                        VatRate = vatRate,
                        Discount = itemDto.Discount,
                        LineTotal = lineTotal,
                        ItemId = item?.Id
                    };

                    invoice.InvoiceItems.Add(invoiceItem);
                }

                // Calculate totals
                var (subtotal, vatAmount, totalDiscount, total) = CalculateInvoiceTotals(invoice.InvoiceItems.ToList());
                invoice.Subtotal = subtotal;
                invoice.VatAmount = vatAmount;
                invoice.TotalDiscount = totalDiscount;
                invoice.Total = total + dto.Benefits.GetValueOrDefault();

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                // Handle final status and inventory updates ONLY if not staying in Draft
                if (dto.Status.ToLower() == InvoiceStatus.Paid.ToString().ToLower() || dto.Status.ToLower() == InvoiceStatus.PartialPaid.ToString().ToLower() || dto.Status.ToLower() == InvoiceStatus.Pending.ToString().ToLower())
                {
                    await UpdateInventoryForInvoiceAsync(invoice, dto.InvoiceType);

                    // Update customer engagement date for paid statuses
                    if (dto.Status == InvoiceStatus.Paid.ToString() || dto.Status == InvoiceStatus.PartialPaid.ToString())
                    {
                        invoice.Customer.LastEngagementDate = DateTime.UtcNow;
                    }

                    decimal paymentAmount = 0;
                    if (dto.Status == InvoiceStatus.Paid.ToString())
                    {
                        paymentAmount = invoice.Total;
                        invoice.AmountPaid = paymentAmount;
                        invoice.PaidAt = DateTime.UtcNow;
                    }
                    else if (dto.DownPayment.GetValueOrDefault() > 0)
                    {
                        paymentAmount = dto.DownPayment.Value;
                        invoice.AmountPaid = paymentAmount;
                        // Status might have been determined by installments logic
                    }

                    if (paymentAmount > 0)
                    {
                        // Account Balance Validation for Purchase Invoices
                        if (dto.InvoiceType?.ToLower() == InvoiceTypes.Buy.ToString().ToLower())
                        {
                            var balanceResult = await ValidatePurchaseInvoiceBalanceAsync(dto.PaymentAccountId, paymentAmount);
                            if (!balanceResult.Success)
                            {
                                await transaction.RollbackAsync();
                                return ServiceResult<InvoiceDto>.Failure(balanceResult.ErrorMessage);
                            }
                        }

                        await RegisterPaymentAsync(invoice, paymentAmount, null, null, dto.PaymentAccountId);
                    }

                    if (dto.NumberOfInstallments > 0)
                    {
                        await HandleInstallmentsAsync(invoice, dto);

                        // Status refinement based on DownPayment if not already Paid
                        if (invoice.Status != InvoiceStatus.Paid.ToString())
                        {
                            if (dto.DownPayment.GetValueOrDefault() > 0)
                            {
                                invoice.Status = InvoiceStatus.PartialPaid.ToString();
                            }
                            else
                            {
                                invoice.Status = InvoiceStatus.Pending.ToString();
                            }
                        }
                    }
                }

                // Call PostInvoiceAsync for non-draft, non-cancelled statuses to ensure AR balance is updated immediately
                var activeStatusCreate = new[] { InvoiceStatus.Pending.ToString(), InvoiceStatus.PartialPaid.ToString(), InvoiceStatus.Paid.ToString(), InvoiceStatus.Overdue.ToString(), InvoiceStatus.Posted.ToString() };
                if (activeStatusCreate.Contains(invoice.Status))
                {
                    await _accountingPostingService.PostInvoiceAsync(invoice.Id);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var createdInvoice = await GetInvoiceWithRelations(invoice.Id);
                return ServiceResult<InvoiceDto>.SuccessResult(MapToDto(createdInvoice));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating invoice");
                return ServiceResult<InvoiceDto>.Failure("Failed to create invoice");
            }
        }

        // Helper methods
        private async Task UpdateInventoryForInvoiceAsync(Invoice invoice, string invoiceType)
        {
            foreach (var invoiceItem in invoice.InvoiceItems)
            {
                if (invoiceItem.Item != null && invoiceItem.Item.Type == "product" && !invoiceItem.Item.IsDeleted)
                {
                    if (invoiceType.ToLower() == InvoiceTypes.Buy.ToString().ToLower())
                    {
                        invoiceItem.Item.Quantity += invoiceItem.Quantity;
                    }
                    else if (invoiceType.ToLower() == InvoiceTypes.Sell.ToString().ToLower())
                    {
                        if (invoiceItem.Item.Quantity < invoiceItem.Quantity)
                        {
                            throw new Exception($"Insufficient quantity for item: {invoiceItem.Item.Name}. Available: {invoiceItem.Item.Quantity}, Requested: {invoiceItem.Quantity}");
                        }
                        invoiceItem.Item.Quantity -= invoiceItem.Quantity;
                    }
                }
            }
        }

        private async Task HandleInstallmentsAsync(Invoice invoice, InvoiceCreateDto dto)
        {
            var remaining = invoice.Total - dto.DownPayment.GetValueOrDefault();

            if (dto.Installments == null || !dto.Installments.Any())
            {
                var installmentAmount = Math.Round(remaining / dto.NumberOfInstallments.Value, 2);
                for (int i = 1; i <= dto.NumberOfInstallments; i++)
                {
                    var dueDate = dto.IssueDate.AddMonths(i);
                    _context.Installments.Add(new Installment
                    {
                        TenantId = TenantId,
                        InvoiceId = invoice.Id,
                        Amount = installmentAmount,
                        DueDate = dueDate
                    });
                }
            }
            else
            {
                var orderedInstallments = dto.Installments.OrderBy(i => i.DueDate).ToList();
                foreach (var instDto in orderedInstallments)
                {
                    _context.Installments.Add(new Installment
                    {
                        TenantId = TenantId,
                        InvoiceId = invoice.Id,
                        DueDate = instDto.DueDate,
                        Amount = instDto.Amount
                    });
                }
            }
        }
        #endregion
        public async Task<ServiceResult<InvoiceDto>> GetPublicInvoiceAsync(Guid invoiceId)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Installments)
                    .Include(i => i.InvoiceItems)
                    .ThenInclude(ii => ii.Item)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null)
                    return ServiceResult<InvoiceDto>.Failure("Invoice not found");

                // Fetch company data based on the invoice's TenantId
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.TenantId == invoice.TenantId);

                if (company == null)
                    return ServiceResult<InvoiceDto>.Failure("Company data not found for this invoice");

                // Map both invoice and company data to the DTO
                return ServiceResult<InvoiceDto>.SuccessResult(MapToDto(invoice, company));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving invoice with ID: {invoiceId}");
                return ServiceResult<InvoiceDto>.Failure("Failed to retrieve invoice");
            }
        }
        public async Task<ServiceResult<InvoiceDto>> GetInvoiceAsync(Guid invoiceId)
        {
            try
            {
                var invoice = await GetInvoiceWithRelations(invoiceId);
                if (invoice == null)
                    return ServiceResult<InvoiceDto>.Failure("Invoice not found");

                return ServiceResult<InvoiceDto>.SuccessResult(MapToDto(invoice));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving invoice with ID: {invoiceId}");
                return ServiceResult<InvoiceDto>.Failure("Failed to retrieve invoice");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<InvoiceDto>>> GetInvoicesAsync(
            InvoiceFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var baseQuery = _context.Invoices
                    .Where(i => i.TenantId == TenantId)
                    .AsQueryable();

                // Apply filters to base query
                var filteredQuery = ApplyFilters(baseQuery, filter);

                // Get total count for pagination
                var totalCount = await filteredQuery.CountAsync();

                // Calculate statistics using database aggregation (more efficient)
                var statsQuery = filteredQuery.GroupBy(i => 1).Select(g => new
                {
                    total = g.Count(),
                    draft = g.Count(i => i.Status == "Draft"),
                    pending = g.Count(i => i.Status == InvoiceStatus.Pending.ToString() && i.DueDate >= DateTime.Now),
                    cancelled = g.Count(i => i.Status == InvoiceStatus.Cancelled.ToString()),
                    paid = g.Count(i => i.Status == "Paid"),
                    overdue = g.Count(i => i.Status == "Pending" && i.DueDate < DateTime.Now),
                    totalAmount = g.Sum(i => i.Total),
                    averageAmount = g.Average(i => i.Total),
                    minAmount = g.Min(i => i.Total),
                    maxAmount = g.Max(i => i.Total)
                });

                var stats = await statsQuery.FirstOrDefaultAsync() ?? new
                {
                    total = 0,
                    draft = 0,
                    pending = 0,
                    cancelled = 0,
                    paid = 0,
                    overdue = 0,
                    totalAmount = 0m,
                    averageAmount = 0m,
                    minAmount = 0m,
                    maxAmount = 0m
                };

                // Get paginated data with includes
                var invoices = await filteredQuery
                    .Include(i => i.Customer)
                     .Include(i => i.Installments)
                    .Include(i => i.Project)
                    .Include(i => i.InvoiceItems)
                    .ThenInclude(ii => ii.Item)
                    .OrderByDescending(i => i.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var invoiceDtos = invoices.Select(MapToDto).ToList();

                return ServiceResult<PagedResponseDto<InvoiceDto>>.SuccessResult(
                    new PagedResponseDto<InvoiceDto>
                    {
                        Data = invoiceDtos,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices with filters: {@Filter}", filter);
                return ServiceResult<PagedResponseDto<InvoiceDto>>.Failure("Failed to retrieve invoices");
            }
        }

        private IQueryable<Invoice> ApplyFilters(IQueryable<Invoice> query, InvoiceFilterDto filter)
        {
            // Search filter - enhanced to search in multiple fields
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var searchTerm = filter.Search.ToLower();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(searchTerm) ||
                    i.Customer.Name.ToLower().Contains(searchTerm) ||
                    (i.Notes != null && i.Notes.ToLower().Contains(searchTerm)) ||
                    (i.Terms != null && i.Terms.ToLower().Contains(searchTerm))
                );
            }

            // Customer filter
            if (filter.CustomerId.HasValue)
                query = query.Where(i => i.CustomerId == filter.CustomerId.Value);

            // Status filter
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(i => i.Status == filter.Status);

            // Branch filter
            if (filter.BranchId.HasValue)
                query = query.Where(i => i.BranchId == filter.BranchId.Value);

            // Invoice type filter
            if (!string.IsNullOrWhiteSpace(filter.InvoiceType))
                query = query.Where(i => i.InvoiceType.ToLower() == filter.InvoiceType.ToLower());

            // Date range filters
            if (filter.FromDate.HasValue)
                query = query.Where(i => i.IssueDate >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                query = query.Where(i => i.IssueDate <= filter.ToDate.Value);

            // Price range filters
            if (filter.minimumPrice.HasValue)
                query = query.Where(i => i.Total >= filter.minimumPrice.Value);

            if (filter.maximumPrice.HasValue)
                query = query.Where(i => i.Total <= filter.maximumPrice.Value);

            // Project filter
            if (filter.ProjectId.HasValue)
                query = query.Where(i => i.ProjectId == filter.ProjectId.Value);

            return query;
        }

        public async Task<ServiceResult<InvoiceDto>> UpdateInvoiceAsync(Guid invoiceId, InvoiceUpdateDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.InvoiceItems)
                    .Include(i => i.Installments) // Include installments
                    .Include(i => i.Project)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == TenantId);

                if (invoice == null)
                    return ServiceResult<InvoiceDto>.Failure("Invoice not found");

                // Prevent changes to cancelled invoices
                if (invoice.Status == InvoiceStatus.Cancelled.ToString())
                    return ServiceResult<InvoiceDto>.Failure("Cannot update a cancelled invoice.");

                // Validate customer if changed
                if (dto.CustomerId.HasValue && dto.CustomerId.Value != invoice.CustomerId)
                {
                    var customerExists = await _context.Customers
                        .AnyAsync(c => c.Id == dto.CustomerId.Value && c.TenantId == TenantId);

                    if (!customerExists)
                        return ServiceResult<InvoiceDto>.Failure("Customer not found");
                }

                // Get company for default values
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.TenantId == TenantId);

                // Update basic fields
                invoice.CustomerId = dto.CustomerId ?? invoice.CustomerId;
                invoice.IssueDate = dto.IssueDate ?? invoice.IssueDate;
                invoice.DueDate = dto.DueDate ?? invoice.DueDate;
                invoice.Notes = dto.Notes ?? invoice.Notes;
                invoice.Terms = dto.Terms ?? invoice.Terms;
                invoice.Status = dto.Status ?? invoice.Status;
                if (dto.File != null && dto.File.Length > 0)
                {
                    if (!string.IsNullOrEmpty(invoice.AttachmentUrl))
                    {
                        DeleteFile(invoice.AttachmentUrl);
                    }
                    dto.AttachmentUrl = await SaveFile(dto.File, "invoices");
                }

                invoice.InvoiceType = dto.InvoiceType ?? invoice.InvoiceType;
                invoice.BranchId = dto.BranchId ?? invoice.BranchId;
                // Project status check
                if (dto.ProjectId.HasValue)
                {
                    var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId.Value && p.TenantId == TenantId);
                    if (project == null)
                        return ServiceResult<InvoiceDto>.Failure("Project not found.");

                    if (project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Cancelled)
                        return ServiceResult<InvoiceDto>.Failure($"Cannot link invoice to a {project.Status.ToString().ToLower()} project.");

                    // Sales invoice restriction: Only one per project
                    var targetInvoiceType = dto.InvoiceType ?? invoice.InvoiceType;
                    if (targetInvoiceType?.ToLower() == InvoiceTypes.Sell.ToString().ToLower() ||
                        targetInvoiceType?.ToLower() == "sales" ||
                        targetInvoiceType?.ToLower() == "sale")
                    {
                        var existingSalesInvoice = await _context.Invoices
                            .AnyAsync(i => i.ProjectId == dto.ProjectId.Value &&
                                           i.Id != invoiceId &&
                                           i.TenantId == TenantId &&
                                           (i.InvoiceType.ToLower() == InvoiceTypes.Sell.ToString().ToLower() ||
                                            i.InvoiceType.ToLower() == "sales" ||
                                            i.InvoiceType.ToLower() == "sale"));

                        if (existingSalesInvoice)
                            return ServiceResult<InvoiceDto>.Failure("A sales invoice already exists for this project. Each project can only have one sales invoice.");
                    }

                    invoice.ProjectId = dto.ProjectId;
                }
                else
                {
                    invoice.ProjectId = null;
                }
                invoice.InvoiceNumber = dto.InvoiceNumber ?? invoice.InvoiceNumber;
                invoice.AttachmentUrl = dto.AttachmentUrl ?? invoice.AttachmentUrl;
                invoice.UpdatedAt = DateTime.UtcNow;

                if (dto.Items == null || !dto.Items.Any())
                {
                    dto.Items = ParseJsonFromForm<InvoiceItemCreateDto>("items");
                }

                if ((dto.Installments == null || !dto.Installments.Any()) && dto.NumberOfInstallments > 0)
                {
                    dto.Installments = ParseJsonFromForm<InstallmentUpdateInvoiceDto>("installments");
                }


                // Handle benefits if provided
                if (dto.Benefits.HasValue)
                {
                    invoice.Benefits = dto.Benefits.Value;
                }

                // Handle down payment if provided
                if (dto.DownPayment.HasValue)
                {
                    invoice.DownPayment = dto.DownPayment.Value;
                }

                // Update items if provided
                if (dto.Items != null)
                {
                    // Step 1: Revert inventory for existing items
                    var existingItemsToRemove = await _context.InvoiceItems
                        .Where(ii => ii.InvoiceId == invoiceId)
                        .ToListAsync();

                    foreach (var existingItem in existingItemsToRemove)
                    {
                        if (existingItem.ItemId.HasValue)
                        {
                            var item = await _context.Items.FindAsync(existingItem.ItemId.Value);
                            if (item != null)
                            {
                                // Revert previous inventory change
                                if (invoice.InvoiceType == InvoiceTypes.Buy.ToString())
                                {
                                    item.Quantity -= existingItem.Quantity;
                                }
                                else
                                {
                                    item.Quantity += existingItem.Quantity;
                                }
                            }
                        }
                    }

                    // Step 2: Remove existing items explicitly
                    if (existingItemsToRemove.Any())
                    {
                        _context.InvoiceItems.RemoveRange(existingItemsToRemove);
                        await _context.SaveChangesAsync(); // Save the removal
                    }

                    // Step 3: Create and add new items
                    var newInvoiceItems = new List<InvoiceItem>();

                    foreach (var itemDto in dto.Items)
                    {
                        var item = itemDto.ItemId != null
                            ? await _context.Items.FindAsync(itemDto.ItemId)
                            : null;

                        // Apply new inventory changes
                        if (item != null)
                        {
                            if (invoice.InvoiceType == InvoiceTypes.Buy.ToString())
                            {
                                item.Quantity += itemDto.Quantity;
                            }
                            else
                            {
                                item.Quantity -= itemDto.Quantity;
                            }
                        }

                        var unitPrice = itemDto.UnitPrice > 0 ? itemDto.UnitPrice : item?.UnitPrice ?? 0;
                        var vatRate = itemDto.VatRate > 0 ? itemDto.VatRate :
                            item?.VatRate ?? company?.DefaultVatRate ?? 0.0m;

                        if (string.IsNullOrWhiteSpace(itemDto.Description) && string.IsNullOrWhiteSpace(item?.Name))
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<InvoiceDto>.Failure("Item description is required");
                        }

                        var lineTotal = CalculateLineTotal(
                            itemDto.Quantity,
                            unitPrice,
                            itemDto.Discount,
                            vatRate.GetValueOrDefault());

                        var newInvoiceItem = new InvoiceItem
                        {
                            Id = Guid.NewGuid(),
                            TenantId = TenantId,
                            InvoiceId = invoiceId, // Explicit foreign key
                            Description = itemDto.Description ?? item?.Name,
                            Quantity = itemDto.Quantity,
                            UnitPrice = unitPrice,
                            VatRate = vatRate,
                            Discount = itemDto.Discount,
                            LineTotal = lineTotal,
                            ItemId = item?.Id
                        };

                        newInvoiceItems.Add(newInvoiceItem);
                    }

                    // Step 4: Add new items to context
                    await _context.InvoiceItems.AddRangeAsync(newInvoiceItems);

                    // Step 5: Recalculate totals
                    var (subtotal, vatAmount, totalDiscount, total) = CalculateInvoiceTotals(newInvoiceItems);
                    invoice.Subtotal = subtotal;
                    invoice.VatAmount = vatAmount;
                    invoice.TotalDiscount = totalDiscount;
                    invoice.Total = total + invoice.Benefits.GetValueOrDefault();
                }

                // Handle installments if provided - IMPROVED LOGIC
                if (dto.Installments != null || dto.NumberOfInstallments.HasValue)
                {
                    await HandleInstallmentsUpdate(invoice, dto);
                }

                // Handle paid invoice status
                if (dto.Status == InvoiceStatus.Paid.ToString())
                {
                    decimal amountToPay = invoice.Total - (invoice.AmountPaid ?? 0);
                    invoice.AmountPaid = invoice.Total;
                    invoice.Status = InvoiceStatus.Paid.ToString();

                    if (amountToPay > 0)
                    {
                        // Account Balance Validation for Purchase Invoices
                        if (invoice.InvoiceType?.ToLower() == InvoiceTypes.Buy.ToString().ToLower())
                        {
                            var balanceResult = await ValidatePurchaseInvoiceBalanceAsync(dto.PaymentAccountId, amountToPay);
                            if (!balanceResult.Success)
                            {
                                await transaction.RollbackAsync();
                                return ServiceResult<InvoiceDto>.Failure(balanceResult.ErrorMessage);
                            }
                        }

                        await RegisterPaymentAsync(invoice, amountToPay);
                    }
                }

                // Trigger project status check if project is linked
                if (invoice.ProjectId.HasValue && invoice.Status == InvoiceStatus.Paid.ToString())
                {
                    await _projectService.CompleteProjectIfInvoicesPaidAsync(invoice.ProjectId.Value);
                }

                // Step 6: Update the invoice
                _context.Invoices.Update(invoice);

                // Call PostInvoiceAsync for non-draft, non-cancelled statuses to ensure AR balance is updated immediately
                var activeStatusUpdate = new[] { InvoiceStatus.Pending.ToString(), InvoiceStatus.PartialPaid.ToString(), InvoiceStatus.Paid.ToString(), InvoiceStatus.Overdue.ToString(), InvoiceStatus.Posted.ToString() };
                if (activeStatusUpdate.Contains(invoice.Status))
                {
                    await _accountingPostingService.PostInvoiceAsync(invoice.Id);
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                var updatedInvoice = await _context.Invoices
                    .Include(i => i.InvoiceItems)
                    .Include(i => i.Installments)
                    .FirstAsync(i => i.Id == invoiceId);

                return ServiceResult<InvoiceDto>.SuccessResult(MapToDto(updatedInvoice));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating invoice with ID: {invoiceId}. Error: {ex.Message}");
                return ServiceResult<InvoiceDto>.Failure($"Failed to update invoice: {ex.Message}");
            }
        }

        private async Task HandleInstallmentsUpdate(Invoice invoice, InvoiceUpdateDto dto)
        {
            var existingInstallments = await _context.Installments
                .Where(i => i.InvoiceId == invoice.Id)
                .ToListAsync();

            // Case 1: No installments provided and NumberOfInstallments is 0 or null
            if ((dto.Installments == null || !dto.Installments.Any()) &&
                (!dto.NumberOfInstallments.HasValue || dto.NumberOfInstallments.Value == 0))
            {
                // Remove all existing installments
                if (existingInstallments.Any())
                {
                    _context.Installments.RemoveRange(existingInstallments);
                }

                // Update invoice status if no installments
                if (invoice.DownPayment > 0 && invoice.DownPayment < invoice.Total)
                {
                    invoice.AmountPaid = invoice.DownPayment;
                    invoice.Status = InvoiceStatus.PartialPaid.ToString();
                }
                else if (invoice.DownPayment >= invoice.Total)
                {
                    invoice.AmountPaid = invoice.Total;
                    invoice.Status = InvoiceStatus.Paid.ToString();

                    await RegisterPaymentAsync(invoice, invoice.Total, "Cash", null, dto.PaymentAccountId);
                }
                else
                {
                    invoice.Status = InvoiceStatus.Pending.ToString();
                }
                return;
            }

            // Case 2: Auto-generate installments based on NumberOfInstallments
            if (dto.NumberOfInstallments.HasValue && dto.NumberOfInstallments.Value > 0 &&
                (dto.Installments == null || !dto.Installments.Any()))
            {
                // Remove existing installments
                if (existingInstallments.Any())
                {
                    _context.Installments.RemoveRange(existingInstallments);
                }

                // Generate new installments
                var remaining = invoice.Total - invoice.DownPayment.GetValueOrDefault();
                var installmentAmount = Math.Round(remaining / dto.NumberOfInstallments.Value, 2);
                var lastInstallmentAmount = remaining - (installmentAmount * (dto.NumberOfInstallments.Value - 1));

                for (int i = 1; i <= dto.NumberOfInstallments.Value; i++)
                {
                    var dueDate = invoice.IssueDate.AddMonths(i);
                    var amount = i == dto.NumberOfInstallments.Value ? lastInstallmentAmount : installmentAmount;

                    _context.Installments.Add(new Installment
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        InvoiceId = invoice.Id,
                        Amount = amount,
                        DueDate = dueDate,
                        Status = InstallmentStatus.Pending.ToString()
                    });
                }
            }
            // Case 3: Specific installments provided
            else if (dto.Installments != null && dto.Installments.Any())
            {
                // Handle update/create/delete for specific installments
                var installmentsToDelete = new List<Installment>();
                var installmentsToUpdate = new List<Installment>();
                var installmentsToCreate = new List<InstallmentCreateDto>();

                // Check if installments have IDs (meaning they are updates)
                var installmentsWithIds = dto.Installments.Where(i => i.Id.HasValue).ToList();
                var installmentsWithoutIds = dto.Installments.Where(i => !i.Id.HasValue).ToList();

                // Handle updates for existing installments
                foreach (var instDto in installmentsWithIds)
                {
                    var existingInstallment = existingInstallments.FirstOrDefault(ei => ei.Id == instDto.Id.Value);
                    if (existingInstallment != null)
                    {
                        existingInstallment.Amount = instDto.Amount;
                        existingInstallment.DueDate = instDto.DueDate;
                        installmentsToUpdate.Add(existingInstallment);
                    }
                }

                // Find installments to delete (existing but not in the update list)
                var installmentIdsToKeep = installmentsWithIds.Select(i => i.Id.Value).ToList();
                installmentsToDelete = existingInstallments.Where(ei => !installmentIdsToKeep.Contains(ei.Id)).ToList();

                // Handle new installments (without IDs)
                foreach (var instDto in installmentsWithoutIds)
                {
                    _context.Installments.Add(new Installment
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        InvoiceId = invoice.Id,
                        DueDate = instDto.DueDate,
                        Amount = instDto.Amount,
                        Status = InstallmentStatus.Pending.ToString()
                    });
                }

                // Apply changes
                if (installmentsToDelete.Any())
                {
                    _context.Installments.RemoveRange(installmentsToDelete);
                }

                if (installmentsToUpdate.Any())
                {
                    _context.Installments.UpdateRange(installmentsToUpdate);
                }
            }

            // Update invoice status based on installments and down payment
            var hasInstallments = await _context.Installments.AnyAsync(i => i.InvoiceId == invoice.Id);

            if (hasInstallments || invoice.DownPayment > 0)
            {
                if (invoice.DownPayment > 0 && invoice.DownPayment < invoice.Total)
                {
                    invoice.AmountPaid = invoice.DownPayment;
                    invoice.Status = InvoiceStatus.PartialPaid.ToString();
                }
                else if (invoice.DownPayment >= invoice.Total)
                {
                    invoice.AmountPaid = invoice.Total;
                    invoice.Status = InvoiceStatus.Paid.ToString();

                    await RegisterPaymentAsync(invoice, invoice.Total);
                }
                else
                {
                    invoice.Status = InvoiceStatus.Pending.ToString();
                }
            }
        }
        public async Task<ServiceResult<bool>> DeleteInvoiceAsync(Guid invoiceId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == TenantId);

                if (invoice == null)
                    return ServiceResult<bool>.Failure("Invoice not found");

                // Prevent deletion of cancelled invoices
                if (invoice.Status == InvoiceStatus.Cancelled.ToString())
                    return ServiceResult<bool>.Failure("Cannot delete a cancelled invoice.");

                // Soft delete (or hard delete if preferred)
                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting invoice with ID: {invoiceId}");
                return ServiceResult<bool>.Failure("Failed to delete invoice");
            }
        }
        public async Task<ServiceResult<bool>> UpdateInvoiceStatusAsync(Guid invoiceId, string status)
        {
            try
            {
                var invoice = await _context.Invoices
                                .Include(i => i.Customer)
                                .Include(i => i.InvoiceItems)
                                .ThenInclude(ii => ii.Item)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == TenantId);

                if (invoice == null)
                    return ServiceResult<bool>.Failure("Invoice not found");

                string oldStatus = invoice.Status;

                // Validate status transitions
                if (oldStatus == InvoiceStatus.Cancelled.ToString() && status != InvoiceStatus.Cancelled.ToString())
                    return ServiceResult<bool>.Failure("Cannot change status from Cancelled");

                if (oldStatus == InvoiceStatus.Paid.ToString() && status != InvoiceStatus.Cancelled.ToString())
                    return ServiceResult<bool>.Failure("Can only cancel a paid invoice");

                invoice.Status = status;
                invoice.UpdatedAt = DateTime.UtcNow;


                // Handle Paid status
                if (status == InvoiceStatus.Paid.ToString())
                {
                    invoice.Customer.LastEngagementDate = DateTime.UtcNow;
                    invoice.PaidAt = DateTime.UtcNow;

                    // Determine the amount to register (if any)
                    decimal amountToPay = invoice.Total - (invoice.AmountPaid ?? 0);

                    // Account Balance Validation for Purchase Invoices
                    if (invoice.InvoiceType?.ToLower() == InvoiceTypes.Buy.ToString().ToLower() && amountToPay > 0 && oldStatus != InvoiceStatus.Paid.ToString())
                    {
                        var balanceResult = await ValidatePurchaseInvoiceBalanceAsync(null, amountToPay);
                        if (!balanceResult.Success)
                        {
                            return ServiceResult<bool>.Failure(balanceResult.ErrorMessage);
                        }
                    }

                    // Ensure AmountPaid is set to Total when marked as Paid
                    if (invoice.AmountPaid < invoice.Total)
                    {
                        invoice.AmountPaid = invoice.Total;
                    }

                    // Register the payment (Transaction + Accounting Entry)
                    // We only trigger this if transitioning to Paid and there's a balance to pay
                    if (oldStatus != InvoiceStatus.Paid.ToString() && amountToPay > 0)
                    {
                        await RegisterPaymentAsync(invoice, amountToPay);
                    }

                    // If coming from Cancelled or Draft, process inventory
                    if (oldStatus == InvoiceStatus.Cancelled.ToString() || oldStatus == InvoiceStatus.Draft.ToString())
                    {
                        await UpdateInventoryForInvoiceAsync(invoice, invoice.InvoiceType);
                    }

                    // Trigger project status check if project is linked
                    if (invoice.ProjectId.HasValue)
                    {
                        await _projectService.CompleteProjectIfInvoicesPaidAsync(invoice.ProjectId.Value);
                    }
                }
                // Handle Cancelled status
                else if (status == InvoiceStatus.Cancelled.ToString())
                {
                    // Only adjust inventory if coming from live states (not Draft or already Cancelled)
                    var liveStates = new[]
                    {
                        InvoiceStatus.Pending.ToString(),
                        InvoiceStatus.PartialPaid.ToString(),
                        InvoiceStatus.Paid.ToString(),
                        InvoiceStatus.Overdue.ToString()
                    };

                    if (liveStates.Contains(oldStatus))
                    {
                        // Reverse the inventory changes - opposite of original operation
                        if (invoice.InvoiceType.ToLower() == InvoiceTypes.Sell.ToString().ToLower())
                        {
                            // For sell invoices, add back the quantity when cancelled
                            foreach (var item in invoice.InvoiceItems) if (item.Item != null) item.Item.Quantity += item.Quantity;
                        }
                        else
                        {
                            // For purchase invoices, subtract the quantity when cancelled
                            foreach (var item in invoice.InvoiceItems) if (item.Item != null) item.Item.Quantity -= item.Quantity;
                        }

                        // Handle Financial Reversal (Accounting)
                        try
                        {
                            // Check if invoice was posted
                            if (await _accountingPostingService.IsInvoicePostedAsync(invoiceId))
                            {
                                // Find the journal entry associated with this invoice
                                var journalEntry = await _context.JournalEntries
                                    .FirstOrDefaultAsync(je => je.ReferenceId == invoiceId &&
                                                             je.ReferenceType == JournalEntryReferenceType.Invoice &&
                                                             je.TenantId == TenantId);

                                if (journalEntry != null)
                                {
                                    // Reverse the journal entry
                                    var reversalResult = await _accountingService.ReverseJournalEntryAsync(journalEntry.Id);
                                    if (reversalResult.Success)
                                    {
                                        _logger.LogInformation("Successfully reversed journal entry {JournalEntryId} for cancelled invoice {InvoiceId}", journalEntry.Id, invoiceId);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Failed to reverse journal entry {JournalEntryId} for cancelled invoice {InvoiceId}: {ErrorMessage}", journalEntry.Id, invoiceId, reversalResult.ErrorMessage);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error occurred during financial reversal for cancelled invoice {InvoiceId}", invoiceId);
                            // We continue with cancellation even if accounting reversal fails, but log the error
                        }
                    }
                }
                // Handle Partial Paid status
                else if (status == InvoiceStatus.PartialPaid.ToString())
                {
                    invoice.Customer.LastEngagementDate = DateTime.UtcNow;

                    // If coming from Cancelled, process inventory like a new invoice
                    if (oldStatus == InvoiceStatus.Cancelled.ToString())
                    {
                        await UpdateInventoryForInvoiceAsync(invoice, invoice.InvoiceType);
                    }
                }
                // Handle Draft status (reverting to draft)
                else if (status == InvoiceStatus.Draft.ToString())
                {
                    // If reverting from live state to draft, reverse inventory changes
                    var liveStates = new[]
                    {
                        InvoiceStatus.Pending.ToString(),
                        InvoiceStatus.PartialPaid.ToString(),
                        InvoiceStatus.Paid.ToString(),
                        InvoiceStatus.Overdue.ToString(),
                        InvoiceStatus.Cancelled.ToString()
                    };

                    if (liveStates.Contains(oldStatus))
                    {
                        // Reverse inventory changes - same logic as cancellation
                        if (invoice.InvoiceType.ToLower() == InvoiceTypes.Sell.ToString().ToLower())
                        {
                            foreach (var item in invoice.InvoiceItems) if (item.Item != null) item.Item.Quantity += item.Quantity;
                        }
                        else
                        {
                            foreach (var item in invoice.InvoiceItems) if (item.Item != null) item.Item.Quantity -= item.Quantity;
                        }
                    }
                }

                // Post to AR if transitioning to an active status
                var activeStatusTransitions = new[] { InvoiceStatus.Pending.ToString(), InvoiceStatus.PartialPaid.ToString(), InvoiceStatus.Paid.ToString(), InvoiceStatus.Overdue.ToString(), InvoiceStatus.Posted.ToString() };
                if (activeStatusTransitions.Contains(status))
                {
                    await _accountingPostingService.PostInvoiceAsync(invoiceId);
                }

                await _context.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for invoice with ID: {invoiceId}");
                return ServiceResult<bool>.Failure("Failed to update invoice status");
            }
        }
        public async Task<ServiceResult<bool>> UnPayInstallmentAsync(Guid installmentId)
        {
            var installment = await _context.Installments.Include(i => i.Invoice)
                .FirstOrDefaultAsync(i => i.Id == installmentId && i.TenantId == TenantId);

            if (installment == null) return ServiceResult<bool>.Failure("Installment not found");

            installment.Status = InstallmentStatus.Unpaid.ToString();
            installment.PaidAt = null;

            installment.Invoice.AmountPaid -= installment.Amount;

            // Update invoice status
            if (installment.Invoice.AmountPaid >= installment.Invoice.Total)
            {
                installment.Invoice.Status = InvoiceStatus.Paid.ToString();
                installment.Invoice.PaidAt = DateTime.UtcNow;
            }
            else
            {
                installment.Invoice.Status = InvoiceStatus.PartialPaid.ToString();
            }

            await _context.SaveChangesAsync();
            return ServiceResult<bool>.SuccessResult(true);
        }

        public async Task<ServiceResult<bool>> PayInstallmentAsync(Guid installmentId)
        {
            var installment = await _context.Installments.Include(i => i.Invoice)
                .FirstOrDefaultAsync(i => i.Id == installmentId && i.TenantId == TenantId);

            if (installment == null) return ServiceResult<bool>.Failure("Installment not found");

            installment.Status = InstallmentStatus.Paid.ToString();
            installment.PaidAt = DateTime.UtcNow;

            installment.Invoice.AmountPaid += installment.Amount;

            // Update invoice status
            if (installment.Invoice.AmountPaid >= installment.Invoice.Total)
            {
                installment.Invoice.Status = InvoiceStatus.Paid.ToString();
                installment.Invoice.PaidAt = DateTime.UtcNow;
            }
            else
            {
                installment.Invoice.Status = InvoiceStatus.PartialPaid.ToString();
            }

            // Trigger project status check if project is linked and invoice is now paid
            if (installment.Invoice.ProjectId.HasValue && installment.Invoice.Status == InvoiceStatus.Paid.ToString())
            {
                await _projectService.CompleteProjectIfInvoicesPaidAsync(installment.Invoice.ProjectId.Value);
            }

            // Register the payment
            await RegisterPaymentAsync(installment.Invoice, installment.Amount);

            await _context.SaveChangesAsync();
            return ServiceResult<bool>.SuccessResult(true);
        }

        public async Task<IEnumerable<InstallmentDto>> GetInstallmentsByInvoiceIdAsync(Guid invoiceId)
        {
            return await _context.Installments
                .Where(i => i.InvoiceId == invoiceId)
                .OrderBy(i => i.DueDate)
                .Select(i => new InstallmentDto
                {
                    Id = i.Id,
                    InvoiceId = i.InvoiceId,
                    Amount = i.Amount,
                    DueDate = i.DueDate,
                    Status = i.Status,
                    PaidAt = i.PaidAt
                })
                .ToListAsync();
        }

        public async Task<InstallmentDto> GetInstallmentByIdAsync(Guid installmentId)
        {
            var installment = await _context.Installments.FindAsync(installmentId);
            if (installment == null) return null;

            return new InstallmentDto
            {
                Id = installment.Id,
                InvoiceId = installment.InvoiceId,
                Amount = installment.Amount,
                DueDate = installment.DueDate,
                Status = installment.Status,
                PaidAt = installment.PaidAt
            };
        }


        public async Task<ServiceResult<bool>> SendInvoiceAsync(Guid invoiceId, SendInvoiceDto dto)
        {
            try
            {
                var invoice = await GetInvoiceWithRelations(invoiceId);
                if (invoice == null)
                    return ServiceResult<bool>.Failure("Invoice not found");

                // Generate PDF
                // var pdfBytes = await _pdfService.GenerateInvoicePdf(invoice);

                // Send email
                var email = string.IsNullOrEmpty(dto.Email) ? invoice.Customer.Email : dto.Email;
                //var emailResult = await _emailService.SendInvoiceAsync(
                //    email,
                //    $"Invoice {invoice.InvoiceNumber}",
                //    dto.Message ?? "Please find your invoice attached",
                //    pdfBytes,
                //    $"Invoice_{invoice.InvoiceNumber}.pdf");

                //if (!emailResult.Success)
                //    return emailResult;

                // Update invoice status
                invoice.SentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending invoice");
                return ServiceResult<bool>.Failure("Failed to send invoice");
            }
        }

        // Helper methods
        private async Task<Invoice> GetInvoiceWithRelations(Guid invoiceId)
        {
            return await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Installments)
                .Include(i => i.Project)
                .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.Item)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == TenantId);
        }

        private async Task<Invoice> GetInvoiceWithRelationsWithoutTenant(Guid invoiceId)
        {
            return await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.Item)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
        }

        private string GenerateInvoiceNumber(string prefix, string lastNumber)
        {
            if (string.IsNullOrEmpty(lastNumber))
            {
                return string.IsNullOrEmpty(prefix) ? "1" : $"{prefix}1";
            }

            // Try to extract the numeric part at the end
            var match = System.Text.RegularExpressions.Regex.Match(lastNumber, @"\d+$");
            if (match.Success && long.TryParse(match.Value, out long number))
            {
                var prefixPart = lastNumber.Substring(0, match.Index);
                var nextNumber = number + 1;

                // If the original had leading zeros (like 0001), try to preserve the length if possible
                // but if it's a simple number (1), it will just become (2).
                if (match.Value.StartsWith("0") && match.Value.Length > 1)
                {
                    return $"{prefixPart}{nextNumber.ToString().PadLeft(match.Value.Length, '0')}";
                }

                return $"{prefixPart}{nextNumber}";
            }

            return string.IsNullOrEmpty(prefix) ? "1" : $"{prefix}1";
        }


        private decimal CalculateLineTotal(decimal quantity, decimal unitPrice, decimal discount, decimal vatRate)
        {
            var totalDiscount = (quantity * unitPrice) * discount;
            var totalVatRate = (quantity * unitPrice) * vatRate;
            return ((quantity * unitPrice) + totalVatRate) - totalDiscount;
        }

        // Method to calculate invoice totals dynamically
        private (decimal Subtotal, decimal VatAmount, decimal TotalDiscount, decimal Total) CalculateInvoiceTotals(IEnumerable<InvoiceItem> invoiceItems)
        {
            var subtotal = invoiceItems.Sum(i => i.Quantity.GetValueOrDefault() * i.UnitPrice.GetValueOrDefault());

            var vatAmount = invoiceItems.Sum(i =>
            {
                var lineSubtotal = (i.Quantity.GetValueOrDefault() * i.UnitPrice.GetValueOrDefault()) * (1 - i.Discount.GetValueOrDefault());
                return lineSubtotal * (i.VatRate.GetValueOrDefault());
            });

            var totalDiscount = invoiceItems.Sum(i =>
                (i.Quantity.GetValueOrDefault() * i.UnitPrice.GetValueOrDefault()) * (i.Discount.GetValueOrDefault()));

            var total = (subtotal + vatAmount) - totalDiscount;

            return (subtotal, vatAmount, totalDiscount, total);
        }

        private InvoiceDto MapToDto(Invoice invoice)
        {
            return new InvoiceDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                CustomerId = invoice.CustomerId,
                BranchId = invoice.BranchId,
                CustomerName = invoice.Customer?.Name,
                CustomerPhoneNumber = invoice.Customer?.Phone,
                IssueDate = invoice.IssueDate,
                DueDate = invoice.DueDate,
                Status = invoice.Status == InvoiceStatus.Pending.ToString() && invoice.DueDate < DateTime.Now
                    ? InvoiceStatus.Overdue.ToString()
                    : invoice.Status,
                Subtotal = invoice.Subtotal,
                VatAmount = invoice.VatAmount,
                TotalDiscount = invoice.TotalDiscount,
                Total = invoice.Total,
                Currency = invoice.Currency,
                InvoiceType = invoice.InvoiceType,
                Notes = invoice.Notes,
                Terms = invoice.Terms,
                AmountPaid = invoice.AmountPaid,
                Benefits = invoice.Benefits,
                DownPayment = invoice.DownPayment,
                hasInstallments = invoice.Installments?.Any() ?? false,
                Items = invoice.InvoiceItems.Select(MapToDto).ToList(),
                Installments = invoice?.Installments?.OrderBy(i => i.DueDate)?.Select(MapToDto).ToList(),
                ProjectId = invoice.ProjectId,
                ProjectName = invoice.Project?.Name,
                CreatedAt = invoice.CreatedAt,
                PaidAt = invoice.PaidAt,
                AttachmentUrl = invoice.AttachmentUrl
            };
        }

        private InvoiceDto MapToDto(Invoice invoice, Company company)
        {
            var invoiceDto = MapToDto(invoice); // Use the existing mapping

            // Add company data to the DTO
            invoiceDto.Company = MapToDto(company);

            return invoiceDto;
        }

        private InvoiceItemDto MapToDto(InvoiceItem item) => new()
        {
            Id = item.Id,
            ItemId = item.ItemId,
            ItemName = item.Item?.Name ?? string.Empty,
            Description = item.Description ?? string.Empty,
            Quantity = item.Quantity ?? 0,
            UnitPrice = item.UnitPrice ?? 0,
            VatRate = item.VatRate ?? 0,
            Discount = item.Discount ?? 0,
            LineTotal = item.LineTotal ?? 0
        };


        private InstallmentDto MapToDto(Installment installment) => new()
        {
            Id = installment.Id,
            InvoiceId = installment.InvoiceId,
            Amount = installment.Amount,
            DueDate = installment.DueDate,
            Status = installment.Status,
            PaidAt = installment.PaidAt,
            AttachmentUrl = installment.AttachmentUrl
        };
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
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };

        public async Task<ServiceResult<bool>> RecordPaymentAsync(Guid invoiceId, RecordPaymentDto dto)
        {
            try
            {
                if (dto.File != null && dto.File.Length > 0)
                {
                    dto.AttachmentUrl = await SaveFile(dto.File, "payments");
                }

                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == TenantId);

                if (invoice == null)
                    return ServiceResult<bool>.Failure("Invoice not found.");

                if (dto.Amount <= 0)
                    return ServiceResult<bool>.Failure("Payment amount must be greater than zero.");

                decimal remainingBalance = invoice.Total - (invoice.AmountPaid ?? 0);
                if (dto.Amount > remainingBalance)
                    return ServiceResult<bool>.Failure($"Payment amount exceeds the remaining balance of {remainingBalance}.");

                // Account Balance Validation for Purchase Invoices
                if (invoice.InvoiceType?.ToLower() == InvoiceTypes.Buy.ToString().ToLower())
                {
                    var balanceResult = await ValidatePurchaseInvoiceBalanceAsync(dto.PaymentAccountId, dto.Amount);
                    if (!balanceResult.Success)
                    {
                        return ServiceResult<bool>.Failure(balanceResult.ErrorMessage);
                    }
                }
                // Update paid amount
                invoice.AmountPaid = (invoice.AmountPaid ?? 0) + dto.Amount;
                invoice.UpdatedAt = DateTime.UtcNow;

                // Update status
                if (invoice.AmountPaid >= invoice.Total)
                {
                    invoice.Status = InvoiceStatus.Paid.ToString();
                    invoice.PaidAt = DateTime.UtcNow;
                    invoice.Customer.LastEngagementDate = DateTime.UtcNow;
                }
                else
                {
                    invoice.Status = InvoiceStatus.PartialPaid.ToString();
                }

                // Save changes to invoice
                await _context.SaveChangesAsync();

                // Register payment (Transaction + Accounting Entry)
                await RegisterPaymentAsync(invoice, dto.Amount, dto.PaymentMethod, dto.AttachmentUrl, dto.PaymentAccountId);

                // Trigger project status check if project is linked and invoice is now paid
                if (invoice.ProjectId.HasValue && invoice.Status == InvoiceStatus.Paid.ToString())
                {
                    await _projectService.CompleteProjectIfInvoicesPaidAsync(invoice.ProjectId.Value);
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording partial payment for invoice {InvoiceId}", invoiceId);
                return ServiceResult<bool>.Failure("Failed to record payment.");
            }
        }

        private async Task RegisterPaymentAsync(Invoice invoice, decimal amount, string? paymentMethod = "Cash", string? attachmentUrl = null, Guid? paymentAccountId = null)
        {
            if (amount <= 0) return;

            var isSalesInvoice = invoice.InvoiceType == InvoiceTypes.Sell.ToString() || invoice.InvoiceType?.ToLower() == "sales" || invoice.InvoiceType?.ToLower() == "sale";
            var transactionType = isSalesInvoice ? "PaymentReceived" : "PaymentMade";
            var direction = isSalesInvoice ? "Credit" : "Debit";
            var desc = isSalesInvoice ? "Payment received" : "Payment made";

            // Get current user ID
            var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirst("UserId")?.Value;
            var userId = !string.IsNullOrEmpty(userIdString) ? Guid.Parse(userIdString) : (Guid?)null;

            var transactionResult = await _transactionService.AddTransactionAsync(new Transaction
            {
                TenantId = invoice.TenantId,
                TransactionDate = DateTime.UtcNow,
                Type = transactionType,
                Amount = amount,
                Direction = direction,
                ReferenceId = invoice.Id.ToString(),
                ReferenceType = "Invoice",
                Description = $"{desc} for Invoice #{invoice.InvoiceNumber}",
                PaymentMethod = paymentMethod ?? "Cash",
                AttachmentUrl = attachmentUrl,
                CreatedBy = userId,
                BranchId = invoice.BranchId,
                ProjectId = invoice.ProjectId
            });

            if (transactionResult.Success)
            {
                try
                {
                    // Ensure the invoice itself is posted before the payment
                    var isPosted = await _accountingPostingService.IsInvoicePostedAsync(invoice.Id);
                    if (!isPosted)
                    {
                        _logger.LogInformation("Invoice {InvoiceId} not posted. Posting invoice before recording payment.", invoice.Id);
                        var invoicePosted = await _accountingPostingService.PostInvoiceAsync(invoice.Id);
                        if (!invoicePosted)
                        {
                            _logger.LogWarning("Failed to auto-post invoice {InvoiceId} before payment registration.", invoice.Id);
                            // We continue anyway mapping to the transaction, but accounting entries might be out of sync
                        }
                    }

                    var paymentPosted = await _accountingPostingService.PostPaymentAsync(
                        invoice.Id,
                        amount,
                        transactionResult.Data.Id,
                        paymentAccountId,
                        paymentMethod); // Pass the Transaction ID, Payment Account ID, and Payment Method

                    if (paymentPosted)
                    {
                        _logger.LogInformation("Successfully auto-posted payment for invoice {InvoiceId} to accounting (Amount: {Amount})", invoice.Id, amount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-posting payment for invoice {InvoiceId} to accounting", invoice.Id);
                }
            }
        }

        private string GenerateImageWithFolderName(string? imageName)
        {
            var request = _httpContextAccessor?.HttpContext?.Request;
            return request != null && imageName != null
                ? $"{request.Scheme}://{request.Host.Value}{imageName}"
                : string.Empty;
        }

        private Guid GetCurrentTenantId()
        {
            var tenant = _httpContextAccessor.HttpContext?.Items["CurrentTenant"] as Tenant;
            return tenant?.Id ?? Guid.Empty;
        }

        private async Task<string> SaveFile(IFormFile file, string subFolder)
        {
            var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", subFolder);
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

            return Path.Combine("uploads", subFolder, uniqueFileName);
        }

        private void DeleteFile(string filePath)
        {
            var fullPath = Path.Combine(_hostingEnvironment.WebRootPath, filePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        private List<T>? ParseJsonFromForm<T>(string key)
        {
            try
            {
                var formValue = _httpContextAccessor.HttpContext?.Request.Form[key].ToString();
                if (string.IsNullOrEmpty(formValue)) return null;

                return JsonSerializer.Deserialize<List<T>>(formValue, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse {Key} from Form as JSON string", key);
                return null;
            }
        }
        private async Task<ServiceResult<bool>> ValidatePurchaseInvoiceBalanceAsync(Guid? accountId, decimal amount)
        {
            if (amount <= 0) return ServiceResult<bool>.SuccessResult(true);

            if (!accountId.HasValue)
            {
                // Fallback to default Cash (1000)
                var cashAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountCode == "1000" && a.TenantId == TenantId);
                accountId = cashAccount?.Id;
            }

            if (accountId.HasValue)
            {
                var balanceResult = await _accountingService.GetAccountBalanceAsync(accountId.Value);
                if (balanceResult.Success && balanceResult.Data.Balance < amount)
                {
                    return ServiceResult<bool>.Failure($"Insufficient funds in {balanceResult.Data.AccountName}. Required: {amount:N2}, Available: {balanceResult.Data.Balance:N2} EGP");
                }
            }
            else
            {
                return ServiceResult<bool>.Failure("Payment account not found and default Cash account (1000) is missing.");
            }

            return ServiceResult<bool>.SuccessResult(true);
        }
    }
}

