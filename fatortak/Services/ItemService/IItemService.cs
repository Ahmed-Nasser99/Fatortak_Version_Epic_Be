using fatortak.Dtos.Item;
using fatortak.Dtos.Shared;

namespace fatortak.Services.ItemService
{
    public interface IItemService
    {
        Task<ServiceResult<ItemDto>> CreateItemAsync(ItemCreateDto dto);
        Task<ServiceResult<PagedResponseDto<ItemDto>>> GetItemsAsync(ItemFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<ItemDto>> GetItemAsync(Guid itemId);
        Task<ServiceResult<ItemDto>> UpdateItemAsync(Guid itemId, ItemUpdateDto dto);
        Task<ServiceResult<bool>> ToggleActivation(Guid itemId);
        Task<ServiceResult<bool>> DeleteItemAsync(Guid itemId);
    }
}
