using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Cheque
{
    public class UpdateChequeStatusDto
    {
        [Required]
        public string Status { get; set; }
    }
}
