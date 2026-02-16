using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for updating an existing account
    /// </summary>
    public class AccountUpdateDto
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public bool? IsActive { get; set; }
    }
}

