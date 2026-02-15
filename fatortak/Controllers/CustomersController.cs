using fatortak.Dtos.Customer;
using fatortak.Dtos.Shared;
using fatortak.Services.CustomerService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(
            ICustomerService customerService,
            ILogger<CustomersController> logger)
        {
            _customerService = customerService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<CustomerDto>>>> GetCustomers(
            [FromQuery] CustomerFilterDto filter,
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

                    return BadRequest(ServiceResult<PagedResponseDto<CustomerDto>>.ValidationError(errors));
                }

                var result = await _customerService.GetCustomersAsync(filter, pagination);

                if (!result.Success)
                {
                    return StatusCode(500, result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                return StatusCode(500, ServiceResult<PagedResponseDto<CustomerDto>>.Failure("An error occurred while retrieving customers"));
            }
        }

        [HttpGet("{customerId}")]
        public async Task<ActionResult<ServiceResult<CustomerDto>>> GetCustomer(Guid customerId)
        {
            try
            {
                var result = await _customerService.GetCustomerAsync(customerId);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Customer not found")
                    {
                        return NotFound(result);
                    }
                    return StatusCode(500, result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving customer with ID: {customerId}");
                return StatusCode(500, ServiceResult<CustomerDto>.Failure("An error occurred while retrieving the customer"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<CustomerDto>>> CreateCustomer(CustomerCreateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<CustomerDto>.ValidationError(errors));
                }

                var result = await _customerService.CreateCustomerAsync(dto);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return CreatedAtAction(
                    nameof(GetCustomer),
                    new { customerId = result.Data.Id },
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                return StatusCode(500, ServiceResult<CustomerDto>.Failure("An error occurred while creating the customer"));
            }
        }

        [HttpPost("update/{customerId}")]
        public async Task<ActionResult<ServiceResult<CustomerDto>>> UpdateCustomer(
            Guid customerId,
            CustomerUpdateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<CustomerDto>.ValidationError(errors));
                }

                var result = await _customerService.UpdateCustomerAsync(customerId, dto);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating customer with ID: {customerId}");
                return StatusCode(500, ServiceResult<CustomerDto>.Failure("An error occurred while updating the customer"));
            }
        }

        [HttpPost("delete/{customerId}")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteCustomer(Guid customerId)
        {
            try
            {
                var result = await _customerService.DeleteCustomerAsync(customerId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting customer with ID: {customerId}");
                return StatusCode(500, ServiceResult<bool>.Failure("An error occurred while deleting the customer"));
            }
        }


        [HttpPost("ToggleActivationCustomer/{customerId}")]
        public async Task<ActionResult<ServiceResult<bool>>> ToggleActivation(Guid customerId)
        {
            try
            {
                var result = await _customerService.ToggleActivation(customerId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting customer with ID: {customerId}");
                return StatusCode(500, ServiceResult<bool>.Failure("An error occurred while deleting the customer"));
            }
        }
    }
}