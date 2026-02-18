using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Accounting
{
    public class CreateCustodyAccountDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}
