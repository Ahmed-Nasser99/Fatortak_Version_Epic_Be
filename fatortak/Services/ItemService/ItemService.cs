using fatortak.Context;
using fatortak.Dtos.Item;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.ItemService
{
    public class ItemService : IItemService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ItemService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _hostingEnvironment;


        public ItemService(
            ApplicationDbContext context,
            ILogger<ItemService> logger,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment hostingEnvironment = null)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _hostingEnvironment = hostingEnvironment;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<ItemDto>> CreateItemAsync(ItemCreateDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<ItemDto>.Failure("Name is required");

                if (dto.UnitPrice <= 0)
                    return ServiceResult<ItemDto>.Failure("Unit price must be positive");

                // Check for duplicate name
                var nameExists = await _context.Items
                    .AnyAsync(i => i.TenantId == TenantId &&
                                   i.Name.ToLower() == dto.Name.ToLower() &&
                                   !i.IsDeleted);
                if (nameExists)
                    return ServiceResult<ItemDto>.Failure("An item with this name already exists");

                // Check for duplicate code if code is provided
                if (!string.IsNullOrWhiteSpace(dto.Code))
                {
                    var codeExists = await _context.Items
                        .AnyAsync(i => i.TenantId == TenantId &&
                                     i.Code.ToLower() == dto.Code.ToLower() &&
                                     !i.IsDeleted);
                    if (codeExists)
                        return ServiceResult<ItemDto>.Failure("An item with this code already exists");
                }


                string imagePath = null;
                if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    imagePath = await SaveImage(dto.ImageFile);
                }

                var item = new Item
                {
                    TenantId = TenantId,
                    Code = dto.Code,
                    Name = dto.Name,
                    Description = dto.Description,
                    Quantity = dto.Quantity,
                    InitialQuantity = dto.Quantity,
                    Type = dto.Type,
                    UnitPrice = dto.UnitPrice,
                    PurchaseUnitPrice = dto.PurchaseUnitPrice,
                    Unit = dto.Unit,
                    VatRate = dto.VatRate,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    ImagePath = imagePath,
                    BranchId = dto.BranchId
                };

                await _context.Items.AddAsync(item);
                await _context.SaveChangesAsync();

                return ServiceResult<ItemDto>.SuccessResult(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating item");
                return ServiceResult<ItemDto>.Failure("Failed to create item");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<ItemDto>>> GetItemsAsync(
            ItemFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _context.Items
                    .Where(i => i.TenantId == TenantId && !i.IsDeleted)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(filter.NameOrCode))
                    query = query.Where(i => i.Name.Contains(filter.NameOrCode) ||
                                           i.Code.Contains(filter.NameOrCode));

                if (!string.IsNullOrWhiteSpace(filter.Type))
                    query = query.Where(i => i.Type == filter.Type);

                if (filter.BranchId.HasValue)
                    query = query.Where(i => i.BranchId == filter.BranchId.Value);

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Calculate statistics - efficient version using CountAsync
                var stats = new
                {
                    total = totalCount,
                    products = await query.CountAsync(i => i.Type == "product"),
                    services = await query.CountAsync(i => i.Type == "service")
                };

                // Apply pagination
                var items = await query
                    .OrderByDescending(i => i.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var itemDtos = items.Select(MapToDto).ToList();

                return ServiceResult<PagedResponseDto<ItemDto>>.SuccessResult(
                    new PagedResponseDto<ItemDto>
                    {
                        Data = itemDtos,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items");
                return ServiceResult<PagedResponseDto<ItemDto>>.Failure("Failed to retrieve items");
            }
        }

        public async Task<ServiceResult<ItemDto>> GetItemAsync(Guid itemId)
        {
            try
            {
                var item = await _context.Items
                    .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == TenantId && !i.IsDeleted);

                if (item == null)
                    return ServiceResult<ItemDto>.Failure("Item not found");

                return ServiceResult<ItemDto>.SuccessResult(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving item");
                return ServiceResult<ItemDto>.Failure("Failed to retrieve item");
            }
        }

        public async Task<ServiceResult<ItemDto>> UpdateItemAsync(Guid itemId, ItemUpdateDto dto)
        {
            try
            {
                var item = await _context.Items
                    .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == TenantId);

                if (item == null)
                    return ServiceResult<ItemDto>.Failure("Item not found");

                // Validate name
                if (dto.Name != null && string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<ItemDto>.Failure("Name cannot be empty");

                // Validate unit price
                if (dto.UnitPrice.HasValue && dto.UnitPrice <= 0)
                    return ServiceResult<ItemDto>.Failure("Unit price must be positive");
                
                // Validate purchase unit price
                if (dto.PurchaseUnitPrice.HasValue && dto.PurchaseUnitPrice <= 0)
                    return ServiceResult<ItemDto>.Failure("Unit price must be positive");

                // Validate VAT rate
                if (dto.VatRate.HasValue && (dto.VatRate < 0 || dto.VatRate > 1))
                    return ServiceResult<ItemDto>.Failure("VAT rate must be between 0 and 1");

                // Check for duplicate name (if name is being changed)
                if (dto.Name != null && !string.Equals(item.Name, dto.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var nameExists = await _context.Items
                        .AnyAsync(i => i.TenantId == TenantId &&
                                     i.Id != itemId &&
                                     i.Name.ToLower() == dto.Name.ToLower() &&
                                     !i.IsDeleted);
                    if (nameExists)
                        return ServiceResult<ItemDto>.Failure("An item with this name already exists");
                }

                // Check for duplicate code (if code is being changed)
                if (dto.Code != null && !string.Equals(item.Code, dto.Code, StringComparison.OrdinalIgnoreCase))
                {
                    var codeExists = await _context.Items
                        .AnyAsync(i => i.TenantId == TenantId &&
                                     i.Id != itemId &&
                                     i.Code != null &&
                                     i.Code.ToLower() == dto.Code.ToLower() &&
                                     !i.IsDeleted);
                    if (codeExists)
                        return ServiceResult<ItemDto>.Failure("An item with this code already exists");
                }

                // Handle image removal if requested
                if (dto.RemoveImage == true)
                {
                    if (!string.IsNullOrEmpty(item.ImagePath))
                    {
                        DeleteImage(item.ImagePath);
                        item.ImagePath = null;
                    }
                }

                // Handle new image upload
                if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    // Validate image file
                    if (dto.ImageFile.Length > 5 * 1024 * 1024) // 5MB limit
                        return ServiceResult<ItemDto>.Failure("Image size cannot exceed 5MB");

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(dto.ImageFile.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                        return ServiceResult<ItemDto>.Failure("Only JPG, JPEG, PNG, and GIF images are allowed");

                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(item.ImagePath))
                    {
                        DeleteImage(item.ImagePath);
                    }

                    // Save new image
                    item.ImagePath = await SaveImage(dto.ImageFile);
                }

                // Update properties if they have new values
                if (dto.Code != null) item.Code = dto.Code;
                if (dto.Name != null) item.Name = dto.Name;
                if (dto.Description != null) item.Description = dto.Description;
                if (dto.Type != null) item.Type = dto.Type;
                if (dto.UnitPrice.HasValue) item.UnitPrice = dto.UnitPrice.Value;
                if (dto.PurchaseUnitPrice.HasValue) item.PurchaseUnitPrice = dto.PurchaseUnitPrice.Value;
                if (dto.Unit != null) item.Unit = dto.Unit;
                if (dto.VatRate.HasValue) item.VatRate = dto.VatRate.Value;
                if (dto.Quantity.HasValue) item.Quantity = dto.Quantity.Value;
                if (dto.IsActive.HasValue) item.IsActive = dto.IsActive.Value;
                if (dto.BranchId.HasValue) item.BranchId = dto.BranchId.Value;

                item.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<ItemDto>.SuccessResult(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item");
                return ServiceResult<ItemDto>.Failure("Failed to update item");
            }
        }
        public async Task<ServiceResult<bool>> ToggleActivation(Guid itemId)
        {
            try
            {
                var item = await _context.Items
                    .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == TenantId);

                if (item == null)
                    return ServiceResult<bool>.Failure("Item not found");

                // Soft delete
                item.IsActive = !item.IsActive;
                item.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ToggleActivation Item");
                return ServiceResult<bool>.Failure("Failed to ToggleActivation Item");
            }
        }
        public async Task<ServiceResult<bool>> DeleteItemAsync(Guid itemId)
        {
            try
            {
                var item = await _context.Items
                    .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == TenantId);

                if (item == null)
                    return ServiceResult<bool>.Failure("Item not found");


                if (!string.IsNullOrEmpty(item.ImagePath))
                {
                    DeleteImage(item.ImagePath);
                }
                // Soft delete
                item.IsDeleted = true;
                item.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item");
                return ServiceResult<bool>.Failure("Failed to delete item");
            }
        }


        private ItemDto MapToDto(Item item)
        {
            string imageUrl = null;
            if (!string.IsNullOrEmpty(item.ImagePath))
            {
                var request = _httpContextAccessor.HttpContext.Request;
                imageUrl = $"{request.Scheme}://{request.Host}/{item.ImagePath.Replace("\\", "/")}";
            }
            return new ItemDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                Type = item.Type,
                UnitPrice = item.UnitPrice,
                PurchaseUnitPrice = item.PurchaseUnitPrice,
                Unit = item.Unit,
                Quantity = item.Quantity,
                VatRate = item.VatRate,
                IsActive = item.IsActive,
                ImageUrl = imageUrl,
                BranchId = item.BranchId
            };
        }

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            // Ensure the uploads directory exists
            var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", "items");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }
            return Path.Combine("uploads", "items", uniqueFileName);
        }

        private void DeleteImage(string imagePath)
        {
            var fullPath = Path.Combine(_hostingEnvironment.WebRootPath, imagePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}
