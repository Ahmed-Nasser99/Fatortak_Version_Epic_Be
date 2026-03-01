using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;

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
        public decimal TotalInvoiced { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalAdvances { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal NetProfit { get; set; }
        public decimal Discount { get; set; }
        public List<ProjectLineDto> ProjectLines { get; set; } = new List<ProjectLineDto>();
    }

    public class ProjectLineDto
    {
        public Guid? Id { get; set; }
        public string? SectionName { get; set; }
        [Required]
        public string Description { get; set; }
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        [Range(0.01, double.MaxValue, ErrorMessage = "UnitPrice must be greater than 0")]
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class CreateProjectDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Active;
        public decimal ContractValue { get; set; }
        public decimal? Discount { get; set; }
    }

    public class UpdateProjectDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; } // Can update to link/unlink client
        public ProjectStatus Status { get; set; }
        public decimal ContractValue { get; set; }
        public decimal? Discount { get; set; }
    }
    public class UpdateProjectStatusDto
    {
        public ProjectStatus Status { get; set; }
    }
}
