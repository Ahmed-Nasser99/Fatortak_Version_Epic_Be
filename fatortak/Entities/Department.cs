namespace fatortak.Entities
{
    public class Department : baseEntitiy, ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Tenant Tenant { get; set; }

        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
