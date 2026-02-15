using fatortak.Dtos.Item;
using fatortak.Dtos.Shared;
using fatortak.Services.ItemService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]

    public class ItemsController : ControllerBase
    {
        private readonly IItemService _itemService;
        private readonly ILogger<ItemsController> _logger;

        public ItemsController(
            IItemService itemService,
            ILogger<ItemsController> logger)
        {
            _itemService = itemService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<ItemDto>>>> GetItems(
            [FromQuery] ItemFilterDto filter,
            [FromQuery] PaginationDto pagination)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<PagedResponseDto<ItemDto>>.ValidationError(errors));
                }

                var result = await _itemService.GetItemsAsync(filter, pagination);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items");
                return BadRequest(ServiceResult<PagedResponseDto<ItemDto>>.Failure("An error occurred while retrieving items"));
            }
        }

        [HttpGet("{itemId}")]
        public async Task<ActionResult<ServiceResult<ItemDto>>> GetItem(Guid itemId)
        {
            try
            {
                var result = await _itemService.GetItemAsync(itemId);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Item not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving item with ID: {itemId}");
                return BadRequest(ServiceResult<ItemDto>.Failure("An error occurred while retrieving the item"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<ItemDto>>> CreateItem([FromForm] ItemCreateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<ItemDto>.ValidationError(errors));
                }

                var result = await _itemService.CreateItemAsync(dto);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return CreatedAtAction(
                    nameof(GetItem),
                    new { itemId = result.Data.Id },
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating item");
                return BadRequest(ServiceResult<ItemDto>.Failure("An error occurred while creating the item"));
            }
        }

        [HttpPost("update/{itemId}")]
        public async Task<ActionResult<ServiceResult<ItemDto>>> UpdateItem(
            Guid itemId,
            [FromForm] ItemUpdateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<ItemDto>.ValidationError(errors));
                }

                var result = await _itemService.UpdateItemAsync(itemId, dto);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Item not found")
                    {
                        return NotFound(result);
                    }

                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating item with ID: {itemId}");
                return BadRequest(ServiceResult<ItemDto>.Failure("An error occurred while updating the item"));
            }
        }

        [HttpPost("delete/{itemId}")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteItem(Guid itemId)
        {
            try
            {
                var result = await _itemService.DeleteItemAsync(itemId);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Item not found")
                    {
                        return NotFound(result);
                    }
                    return StatusCode(500, result);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting item with ID: {itemId}");
                return BadRequest(ServiceResult<bool>.Failure("An error occurred while deleting the item"));
            }
        }

        [HttpPost("ToggleActivationItem/{itemId}")]
        public async Task<ActionResult<ServiceResult<bool>>> ToggleActivation(Guid itemId)
        {
            try
            {
                var result = await _itemService.ToggleActivation(itemId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error ToggleActivationItem with ID: {itemId}");
                return StatusCode(500, ServiceResult<bool>.Failure("An error occurred while ToggleActivationItem"));
            }
        }
    }
}