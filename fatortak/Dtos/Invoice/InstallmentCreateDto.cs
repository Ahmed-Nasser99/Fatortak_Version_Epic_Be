namespace fatortak.Dtos.Invoice
{
    public class InstallmentCreateDto
    {
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
    }
}
