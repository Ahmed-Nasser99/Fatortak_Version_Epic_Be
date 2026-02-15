using fatortak.Dtos.Customer;
using fatortak.Dtos.Shared;

namespace fatortak.Services.CustomerService
{
    public interface ICustomerService
    {
        Task<ServiceResult<CustomerDto>> CreateCustomerAsync(CustomerCreateDto dto);
        Task<ServiceResult<PagedResponseDto<CustomerDto>>> GetCustomersAsync(CustomerFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<CustomerDto>> GetCustomerAsync(Guid customerId);
        Task<ServiceResult<CustomerDto>> UpdateCustomerAsync(Guid customerId, CustomerUpdateDto dto);
        Task<ServiceResult<bool>> DeleteCustomerAsync(Guid customerId);
        Task<ServiceResult<bool>> ToggleActivation(Guid customerId);
    }
}
