using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Project
{
    public class CreateProjectWithContractCommand
    {
        [Required]
        public string ProjectName { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        public string? PaymentTerms { get; set; }
        public string? Notes { get; set; }

        [Required]
        public List<ProjectLineDto> Lines { get; set; } = new List<ProjectLineDto>();

        public bool ActivateImmediately { get; set; }
    }

    public class ProjectLineDto
    {
        [Required]
        public string Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal Quantity { get; set; }

        public string? Unit { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "UnitPrice must be greater than 0")]
        public decimal UnitPrice { get; set; }
    }
}
