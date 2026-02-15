namespace fatortak.Entities
{
    public class PayrollItem : baseEntitiy, ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        
        public Guid PayrollId { get; set; }
        public Guid EmployeeId { get; set; }
        
        public decimal BaseSalary { get; set; }
        public decimal CalculatedSalary { get; set; }
        
        public int DaysAttended { get; set; }
        public string CalculationMethod { get; set; } // MainSalary, AttendanceBased
        
        public Tenant Tenant { get; set; }
        public Payroll Payroll { get; set; }
        public Employee Employee { get; set; }
    }
}
