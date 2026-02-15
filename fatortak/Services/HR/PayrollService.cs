using fatortak.Context;
using fatortak.Dtos;
using fatortak.Dtos.Expense;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Services.ExpenseService;
using fatortak.Services.HR.AttendanceService;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.HR
{
    public class PayrollService : IPayrollService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAttendanceService _attendanceService;
        private readonly IExpenseService _expenseService;
        private readonly ILogger<PayrollService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PayrollService(
            ApplicationDbContext context,
            IAttendanceService attendanceService,
            IExpenseService expenseService,
            ILogger<PayrollService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _attendanceService = attendanceService;
            _expenseService = expenseService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<PayrollDto>> GeneratePayrollAsync(GeneratePayrollDto dto)
        {
            try
            {
                // Check if payroll already exists for this month/year
                var existingPayroll = await _context.Payrolls
                    .Include(p => p.PayrollItems)
                    .FirstOrDefaultAsync(p => p.TenantId == _tenantId && p.Month == dto.Month && p.Year == dto.Year);

                if (existingPayroll != null)
                {
                    // If exists and submitted, cannot regenerate
                    if (existingPayroll.Status == "Submitted")
                    {
                        return ServiceResult<PayrollDto>.Failure("Payroll for this month has already been submitted.");
                    }

                    // If draft, delete existing items to regenerate
                    _context.PayrollItems.RemoveRange(existingPayroll.PayrollItems);
                    _context.Payrolls.Remove(existingPayroll);
                    await _context.SaveChangesAsync();
                }

                // Get attendance report to calculate salaries
                var attendanceReportResult = await _attendanceService.GetMonthlyAttendanceReportAsync(dto.Year, dto.Month);
                if (!attendanceReportResult.Success)
                {
                    return ServiceResult<PayrollDto>.Failure($"Failed to get attendance report: {attendanceReportResult.ErrorMessage}");
                }

                var attendanceData = attendanceReportResult.Data;
                var payrollItems = new List<PayrollItem>();
                decimal totalAmount = 0;

                var employees = await _context.Employees.Where(e => e.TenantId == _tenantId).ToListAsync();

                foreach (var emp in employees)
                {
                    var empReport = attendanceData.FirstOrDefault(a => a.EmployeeId == emp.Id);

                    decimal baseSalary = emp.Salary ?? 0;
                    decimal calculatedSalary = 0;
                    int daysAttended = empReport?.PresentDays ?? 0;

                    if (dto.CalculationMethod == "AttendanceBased")
                    {
                        calculatedSalary = empReport?.Salary ?? 0;
                    }
                    else // MainSalary
                    {
                        calculatedSalary = baseSalary;
                    }

                    var item = new PayrollItem
                    {
                        TenantId = _tenantId,
                        EmployeeId = emp.Id,
                        BaseSalary = baseSalary,
                        CalculatedSalary = calculatedSalary,
                        DaysAttended = daysAttended,
                        CalculationMethod = dto.CalculationMethod,
                        Employee = emp
                    };

                    payrollItems.Add(item);
                    totalAmount += calculatedSalary;
                }

                var payroll = new Payroll
                {
                    TenantId = _tenantId,
                    Month = dto.Month,
                    Year = dto.Year,
                    TotalAmount = totalAmount,
                    Status = "Draft",
                    PayrollItems = payrollItems
                };

                _context.Payrolls.Add(payroll);
                await _context.SaveChangesAsync();

                return ServiceResult<PayrollDto>.SuccessResult(MapToDto(payroll));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating payroll");
                return ServiceResult<PayrollDto>.Failure("Failed to generate payroll");
            }
        }

        public async Task<ServiceResult<PayrollDto>> GetPayrollAsync(Guid id)
        {
            try
            {
                var payroll = await _context.Payrolls
                    .Include(p => p.PayrollItems)
                    .ThenInclude(pi => pi.Employee)
                    .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _tenantId);

                if (payroll == null) return ServiceResult<PayrollDto>.Failure("Payroll not found");

                return ServiceResult<PayrollDto>.SuccessResult(MapToDto(payroll));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payroll");
                return ServiceResult<PayrollDto>.Failure("Failed to get payroll");
            }
        }

        public async Task<ServiceResult<List<PayrollDto>>> GetAllPayrollsAsync()
        {
            try
            {
                var payrolls = await _context.Payrolls
                    .Where(p => p.TenantId == _tenantId)
                    .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                    .ToListAsync();

                return ServiceResult<List<PayrollDto>>.SuccessResult(payrolls.Select(p => MapToDto(p)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all payrolls");
                return ServiceResult<List<PayrollDto>>.Failure("Failed to get payrolls");
            }
        }

        public async Task<ServiceResult<PayrollDto>> SubmitPayrollAsync(Guid id)
        {
            try
            {
                var payroll = await _context.Payrolls
                    .Include(p => p.PayrollItems)
                    .ThenInclude(pi => pi.Employee)
                    .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _tenantId);

                if (payroll == null) return ServiceResult<PayrollDto>.Failure("Payroll not found");
                if (payroll.Status == "Submitted") return ServiceResult<PayrollDto>.Failure("Payroll already submitted");

                // Create Expense
                var createExpenseDto = new CreateExpenseDto
                {
                    Date = new DateOnly(payroll.Year, payroll.Month, 1).AddMonths(1).AddDays(-1), // End of month
                    Total = payroll.TotalAmount,
                    Notes = $"Payroll Salaries for {payroll.Month}/{payroll.Year}",
                    File = null
                };

                var expenseResult = await _expenseService.CreateExpenseAsync(createExpenseDto);
                if (!expenseResult.Success)
                {
                    return ServiceResult<PayrollDto>.Failure($"Failed to create expense: {expenseResult.ErrorMessage}");
                }

                payroll.Status = "Submitted";
                payroll.ExpenseId = expenseResult.Data.Id;

                var transaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TenantId == _tenantId && t.ReferenceId == expenseResult.Data.Id.ToString() && t.ReferenceType == "Expense");

                if (transaction != null)
                {
                    payroll.TransactionId = transaction.Id;
                }

                await _context.SaveChangesAsync();

                return ServiceResult<PayrollDto>.SuccessResult(MapToDto(payroll));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting payroll");
                return ServiceResult<PayrollDto>.Failure("Failed to submit payroll");
            }
        }

        public async Task<ServiceResult<bool>> DeletePayrollAsync(Guid id)
        {
            try
            {
                var payroll = await _context.Payrolls
                    .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _tenantId);

                if (payroll == null) return ServiceResult<bool>.Failure("Payroll not found");

                if (payroll.Status == "Submitted")
                {
                    return ServiceResult<bool>.Failure("Cannot delete submitted payroll. Please delete the associated expense first.");
                }

                _context.Payrolls.Remove(payroll);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payroll");
                return ServiceResult<bool>.Failure("Failed to delete payroll");
            }
        }

        private PayrollDto MapToDto(Payroll payroll)
        {
            return new PayrollDto
            {
                Id = payroll.Id,
                Month = payroll.Month,
                Year = payroll.Year,
                TotalAmount = payroll.TotalAmount,
                Status = payroll.Status,
                ExpenseId = payroll.ExpenseId,
                TransactionId = payroll.TransactionId,
                PayrollItems = payroll.PayrollItems?.Select(pi => new PayrollItemDto
                {
                    Id = pi.Id,
                    EmployeeId = pi.EmployeeId,
                    EmployeeName = pi.Employee?.FullName ?? "Unknown",
                    BaseSalary = pi.BaseSalary,
                    CalculatedSalary = pi.CalculatedSalary,
                    DaysAttended = pi.DaysAttended,
                    CalculationMethod = pi.CalculationMethod
                }).ToList() ?? new List<PayrollItemDto>()
            };
        }
    }
}
