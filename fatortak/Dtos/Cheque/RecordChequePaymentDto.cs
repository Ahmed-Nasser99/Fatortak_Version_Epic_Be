using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Cheque
{
    public class RecordChequePaymentDto
    {
        [Required]
        public string ChequeNumber { get; set; }

        [Required]
        public string BankName { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        public Guid? PaymentAccountId { get; set; }
    }
}
