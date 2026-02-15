namespace fatortak.Dtos.Report
{
    public class CashFlowDto
    {
        public decimal CashIn { get; set; }
        public decimal CashOut { get; set; }
        public decimal NetCashFlow { get; set; }
        public decimal TotalPurchaseInvoices { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal? TotalSalaries { get; set; }
        public decimal OutstandingReceivables { get; set; }
    }
}
