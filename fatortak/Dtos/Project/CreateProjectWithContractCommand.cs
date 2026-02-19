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
        public List<fatortak.Dtos.ProjectLineDto> Lines { get; set; } = new List<fatortak.Dtos.ProjectLineDto>();

        public bool ActivateImmediately { get; set; }
    }
}
