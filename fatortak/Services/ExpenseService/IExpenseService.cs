using fatortak.Dtos.Expense;
using fatortak.Dtos.Shared;

namespace fatortak.Services.ExpenseService
{
    public interface IExpenseService
    {
        Task<ServiceResult<PagedResponseDto<ExpenseDto>>> GetAllExpensesAsync(ExpenseFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<ExpenseDto>> GetExpenseByIdAsync(int id);
        Task<ServiceResult<ExpenseDto>> CreateExpenseAsync(CreateExpenseDto expenseDto);
        Task<ServiceResult<ExpenseDto>> UpdateExpenseAsync(int id, UpdateExpenseDto expenseDto);
        Task<ServiceResult<bool>> DeleteExpenseAsync(int id);
    }
}
