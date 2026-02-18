using fatortak.Common.Enum;

namespace fatortak.Dtos
{
    public class ProjectDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public ProjectStatus Status { get; set; }
        public decimal ContractValue { get; set; }
        public string? PaymentTerms { get; set; }
        public string? Notes { get; set; }
        public Guid? InvoiceId { get; set; } // For activated projects
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateProjectDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Active;
        public decimal ContractValue { get; set; }
    }

    public class UpdateProjectDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; } // Can update to link/unlink client
        public ProjectStatus Status { get; set; }
        public decimal ContractValue { get; set; }
    }
    public class UpdateProjectStatusDto
    {
        public ProjectStatus Status { get; set; }
    }
}
