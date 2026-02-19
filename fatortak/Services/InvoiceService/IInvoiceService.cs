using fatortak.Dtos.Invoice;
using fatortak.Dtos.Shared;

namespace fatortak.Services.InvoiceService
{
    public interface IInvoiceService
    {
        Task<ServiceResult<InvoiceDto>> CreateInvoiceFromOcrAsync(OcrInvoiceCreateDto dto);
        Task<ServiceResult<InvoiceDto>> CreateInvoiceAsync(InvoiceCreateDto dto);
        // Task<ServiceResult<byte[]>> GeneratePdfAsync(Guid invoiceId);
        Task<ServiceResult<bool>> SendInvoiceAsync(Guid invoiceId, SendInvoiceDto dto);
        Task<ServiceResult<bool>> UpdateInvoiceStatusAsync(Guid invoiceId, string status);
        Task<ServiceResult<bool>> DeleteInvoiceAsync(Guid invoiceId);
        Task<ServiceResult<InvoiceDto>> UpdateInvoiceAsync(Guid invoiceId, InvoiceUpdateDto dto);
        Task<ServiceResult<PagedResponseDto<InvoiceDto>>> GetInvoicesAsync(
            InvoiceFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<InvoiceDto>> GetInvoiceAsync(Guid invoiceId);
        Task<ServiceResult<InvoiceDto>> GetPublicInvoiceAsync(Guid invoiceId);

        Task<IEnumerable<InstallmentDto>> GetInstallmentsByInvoiceIdAsync(Guid invoiceId);
        Task<InstallmentDto?> GetInstallmentByIdAsync(Guid installmentId);
        Task<ServiceResult<bool>> PayInstallmentAsync(Guid installmentId);
        Task<ServiceResult<bool>> UnPayInstallmentAsync(Guid installmentId);
        Task<ServiceResult<bool>> RecordPaymentAsync(Guid invoiceId, RecordPaymentDto dto);
    }
}
