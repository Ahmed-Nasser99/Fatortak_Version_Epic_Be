using fatortak.Attributes;
using fatortak.Common.Enum;
using fatortak.Dtos.Company;
using fatortak.Dtos.Shared;
using fatortak.Services.CompanyService;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]

public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ILogger<CompaniesController> _logger;

    public CompaniesController(
        ICompanyService companyService,
        ILogger<CompaniesController> logger)
    {
        _companyService = companyService;
        _logger = logger;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ServiceResult<CompanyDto>>> GetCurrentCompany()
    {
        try
        {
            var result = await _companyService.GetCurrentTenantCompanyAsync();

            if (!result.Success)
            {
                if (result.ErrorMessage == "Company not found")
                {
                    return NotFound(result);
                }
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current company");
            return BadRequest(ServiceResult<CompanyDto>.Failure("An error occurred while retrieving company"));
        }
    }

    [HttpGet]
    [AuthorizeRole(RoleEnum.SysAdmin)]
    public async Task<ActionResult<ServiceResult<PagedResponseDto<CompanyDto>>>> GetCompanies(
        [FromQuery] CompanyFilterDto filter,
        [FromQuery] PaginationDto pagination)
    {
        try
        {
            var result = await _companyService.GetCompaniesAsync(filter, pagination);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving companies");
            return BadRequest(ServiceResult<PagedResponseDto<CompanyDto>>.Failure("An error occurred while retrieving companies"));
        }
    }

    [HttpGet("{companyId}")]
    public async Task<ActionResult<ServiceResult<CompanyDto>>> GetCompany(Guid companyId)
    {
        try
        {
            var result = await _companyService.GetCompanyAsync(companyId);

            if (!result.Success)
            {
                if (result.ErrorMessage == "Company not found")
                {
                    return NotFound(result);
                }
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving company with ID: {companyId}");
            return BadRequest(ServiceResult<CompanyDto>.Failure("An error occurred while retrieving company"));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ServiceResult<CompanyDto>>> CreateCompany([FromBody] CompanyCreateDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ServiceResult<CompanyDto>.ValidationError(errors));
            }

            var result = await _companyService.CreateCompanyAsync(dto);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return CreatedAtAction(
                nameof(GetCompany),
                new { companyId = result.Data.Id },
                result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company");
            return BadRequest(ServiceResult<CompanyDto>.Failure("An error occurred while creating company"));
        }
    }

    [HttpPost("update/{companyId}")]
    public async Task<ActionResult<ServiceResult<CompanyDto>>> UpdateCompany(
        Guid companyId,
        [FromBody] CompanyUpdateDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ServiceResult<CompanyDto>.ValidationError(errors));
            }

            var result = await _companyService.UpdateCompanyAsync(companyId, dto);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating company with ID: {companyId}");
            return BadRequest(ServiceResult<CompanyDto>.Failure("An error occurred while updating company"));
        }
    }

    [HttpPost("delete/{companyId}")]
    public async Task<ActionResult<ServiceResult<bool>>> DeleteCompany(Guid companyId)
    {
        try
        {
            var result = await _companyService.DeleteCompanyAsync(companyId);

            if (!result.Success)
            {
                if (result.ErrorMessage == "Company not found")
                {
                    return NotFound(result);
                }
                return StatusCode(500, result);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting company with ID: {companyId}");
            return BadRequest(ServiceResult<bool>.Failure("An error occurred while deleting company"));
        }
    }

    [HttpPost("upload-logo")]
    public async Task<ActionResult<ServiceResult<CompanyDto>>> UploadCompanyLogo([FromForm] UploadCompanyLogoDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ServiceResult<CompanyDto>.ValidationError(errors));
            }

            var result = await _companyService.UploadCompanyLogoAsync(dto);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company");
            return BadRequest(ServiceResult<CompanyDto>.Failure("An error occurred while creating company"));
        }
    }
    [HttpPost("{companyId}/RemoveCompanyLogo")]
    public async Task<ActionResult<ServiceResult<CompanyDto>>> RemoveCompanyLogoDto(Guid companyId)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ServiceResult<CompanyDto>.ValidationError(errors));
            }

            var result = await _companyService.RemoveCompanyLogoAsync(companyId);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company");
            return BadRequest(ServiceResult<CompanyDto>.Failure("An error occurred while creating company"));
        }
    }

    [HttpPost("UpdateCompanyInvoiceTemplateAsync")]
    public async Task<ActionResult<ServiceResult<CompanyDto>>> UpdateCompanyInvoiceTemplateAsync(CompanyUpdateInvoiceTemplateDto model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ServiceResult<CompanyDto>.ValidationError(errors));
            }

            var result = await _companyService.UpdateCompanyInvoiceTemplateAsync(model);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company");
            return BadRequest(ServiceResult<CompanyDto>.Failure("An error occurred while creating company"));
        }
    }
}