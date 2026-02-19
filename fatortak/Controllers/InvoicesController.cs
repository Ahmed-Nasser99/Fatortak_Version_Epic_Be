using fatortak.Dtos.Invoice;
using fatortak.Dtos.Shared;
using fatortak.Services.InvoiceService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(
            IInvoiceService invoiceService,
            ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<InvoiceDto>>>> GetInvoices(
            [FromQuery] InvoiceFilterDto filter,
            [FromQuery] PaginationDto pagination)
        {
            try
            {
                var result = await _invoiceService.GetInvoicesAsync(filter, pagination);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices");
                return BadRequest(ServiceResult<PagedResponseDto<InvoiceDto>>.Failure("Failed to retrieve invoices"));
            }
        }

        [HttpGet("{invoiceId}")]
        public async Task<ActionResult<ServiceResult<InvoiceDto>>> GetInvoice(Guid invoiceId)
        {
            try
            {
                var result = await _invoiceService.GetInvoiceAsync(invoiceId);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Invoice not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving invoice with ID: {invoiceId}");
                return BadRequest(ServiceResult<InvoiceDto>.Failure("Failed to retrieve invoice"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<InvoiceDto>>> CreateInvoice(
            [FromBody] InvoiceCreateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<InvoiceDto>.ValidationError(errors));
                }

                var result = await _invoiceService.CreateInvoiceAsync(dto);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Customer not found" ||
                        result.ErrorMessage == "Company not found")
                    {
                        return BadRequest(result);
                    }
                    return StatusCode(500, result);
                }

                return CreatedAtAction(
                    nameof(GetInvoice),
                    new { invoiceId = result.Data.Id },
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice");
                return BadRequest(ServiceResult<InvoiceDto>.Failure("Failed to create invoice: " + ex.Message));
            }
        }

        [HttpPost("CreateInvoiceFromOcr")]
        public async Task<ActionResult<ServiceResult<InvoiceDto>>> CreateInvoiceFromOcr(
            [FromBody] OcrInvoiceCreateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<InvoiceDto>.ValidationError(errors));
                }

                var result = await _invoiceService.CreateInvoiceFromOcrAsync(dto);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Customer not found" ||
                        result.ErrorMessage == "Company not found")
                    {
                        return BadRequest(result);
                    }
                    return StatusCode(500, result);
                }

                return CreatedAtAction(
                    nameof(GetInvoice),
                    new { invoiceId = result.Data.Id },
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice");
                return BadRequest(ServiceResult<InvoiceDto>.Failure("Failed to create invoice: " + ex.Message));
            }
        }

        [HttpPost("update/{invoiceId}")]
        public async Task<ActionResult<ServiceResult<InvoiceDto>>> UpdateInvoice(
            Guid invoiceId,
            [FromBody] InvoiceUpdateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<InvoiceDto>.ValidationError(errors));
                }

                var result = await _invoiceService.UpdateInvoiceAsync(invoiceId, dto);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Invoice not found")
                    {
                        return NotFound(result);
                    }
                    if (result.ErrorMessage == "Customer not found")
                    {
                        return BadRequest(result);
                    }
                    return StatusCode(500, result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating invoice with ID: {invoiceId}");
                return BadRequest(ServiceResult<InvoiceDto>.Failure("Failed to update invoice: " + ex.Message));
            }
        }

        [HttpPost("delete/{invoiceId}")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteInvoice(Guid invoiceId)
        {
            try
            {
                var result = await _invoiceService.DeleteInvoiceAsync(invoiceId);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Invoice not found")
                    {
                        return NotFound(result);
                    }
                    return StatusCode(500, result);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting invoice with ID: {invoiceId}");
                return BadRequest(ServiceResult<bool>.Failure("Failed to delete invoice: " + ex.Message));
            }
        }

        [HttpPost("{invoiceId}/send")]
        public async Task<ActionResult<ServiceResult<bool>>> SendInvoice(
            Guid invoiceId,
            [FromBody] SendInvoiceDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<bool>.ValidationError(errors));
                }

                var result = await _invoiceService.SendInvoiceAsync(invoiceId, dto);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Invoice not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending invoice with ID: {invoiceId}");
                return BadRequest(ServiceResult<bool>.Failure("Failed to send invoice: " + ex.Message));
            }
        }

        [HttpPost("{id}/payments")]
        public async Task<ActionResult<bool>> RecordPayment(Guid id, [FromBody] RecordPaymentDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _invoiceService.RecordPaymentAsync(id, dto);
                if (!result.Success)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording payment for invoice {InvoiceId}", id);
                return StatusCode(500, new { message = "Failed to record payment" });
            }
        }

        [HttpPost("{invoiceId}/status")]
        public async Task<ActionResult<ServiceResult<bool>>> UpdateInvoiceStatus(
            Guid invoiceId,
            [FromBody] UpdateInvoiceStatusDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<bool>.ValidationError(errors));
                }

                var result = await _invoiceService.UpdateInvoiceStatusAsync(invoiceId, dto.Status);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Invoice not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for invoice with ID: {invoiceId}");
                return BadRequest(ServiceResult<bool>.Failure("Failed to update invoice status: " + ex.Message));
            }
        }

        [AllowAnonymous]
        [HttpGet("public/{invoiceId}")]
        public async Task<ActionResult<ServiceResult<InvoiceDto>>> GetPublicInvoice(Guid invoiceId)
        {
            try
            {
                var invoice = await _invoiceService.GetPublicInvoiceAsync(invoiceId);
                if (invoice == null || !invoice.Success)
                    return NotFound();

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public invoice");
                return StatusCode(500);
            }
        }

        [HttpGet("{invoiceId}/installments")]
        public async Task<IActionResult> GetInstallmentsByInvoiceId(Guid invoiceId)
        {
            var installments = await _invoiceService.GetInstallmentsByInvoiceIdAsync(invoiceId);
            return Ok(installments);
        }

        [HttpGet("installment/{id}")]
        public async Task<IActionResult> GetInstallmentById(Guid id)
        {
            var installment = await _invoiceService.GetInstallmentByIdAsync(id);
            if (installment == null) return NotFound();
            return Ok(installment);
        }

        [HttpPost("installment/pay/{installmentId}")]
        public async Task<IActionResult> PayInstallment(Guid installmentId)
        {
            var installment = await _invoiceService.PayInstallmentAsync(installmentId);
            return Ok(installment);
        }

        [HttpPost("installment/unPay/{installmentId}")]
        public async Task<IActionResult> UnPayInstallment(Guid installmentId)
        {
            var installment = await _invoiceService.UnPayInstallmentAsync(installmentId);
            return Ok(installment);
        }

        // Uncomment when you implement PDF generation in your service
        /*
        [HttpGet("{invoiceId}/pdf")]
        public async Task<IActionResult> GetInvoicePdf(Guid invoiceId)
        {
            try
            {
                var result = await _invoiceService.GeneratePdfAsync(invoiceId);
                
                if (!result.Success)
                {
                    if (result.ErrorMessage == "Invoice not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return File(result.Data, "application/pdf", $"Invoice_{invoiceId}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating PDF for invoice ID: {invoiceId}");
                return BadRequest(ServiceResult<byte[]>.Failure("Failed to generate PDF: " + ex.Message));
            }
        }
        */
    }

    public class UpdateInvoiceStatusDto
    {
        public string Status { get; set; }
    }
}