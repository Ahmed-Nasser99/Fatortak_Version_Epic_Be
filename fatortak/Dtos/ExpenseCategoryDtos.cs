using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos
{
    public class ExpenseCategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? AccountCode { get; set; }
    }

    public class CreateExpenseCategoryDto
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public Guid AccountId { get; set; }
    }

    public class UpdateExpenseCategoryDto
    {
        public string? Name { get; set; }
        public Guid? AccountId { get; set; }
    }
}
