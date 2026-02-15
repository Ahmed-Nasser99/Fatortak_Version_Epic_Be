using fatortak.Dtos.Company;
using fatortak.Dtos.Shared;

namespace fatortak.Services.CompanyService
{
    public interface ICompanyService
    {
        Task<ServiceResult<CompanyDto>> CreateCompanyAsync(CompanyCreateDto dto);
        Task<ServiceResult<CompanyDto>> GetCompanyAsync(Guid companyId);
        Task<ServiceResult<CompanyDto>> GetCurrentTenantCompanyAsync();
        Task<ServiceResult<PagedResponseDto<CompanyDto>>> GetCompaniesAsync(CompanyFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<CompanyDto>> UpdateCompanyAsync(Guid companyId, CompanyUpdateDto dto);
        Task<ServiceResult<bool>> DeleteCompanyAsync(Guid companyId);
        Task<ServiceResult<string>> UploadCompanyLogoAsync(UploadCompanyLogoDto dto);

        Task<ServiceResult<bool>> RemoveCompanyLogoAsync(Guid companyId);
        Task<ServiceResult<CompanyDto>> UpdateCompanyInvoiceTemplateAsync(CompanyUpdateInvoiceTemplateDto dto);
    }
}
