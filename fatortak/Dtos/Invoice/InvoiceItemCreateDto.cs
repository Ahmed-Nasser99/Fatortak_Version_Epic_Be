namespace fatortak.Dtos.Invoice
{
    public class InvoiceItemCreateDto
    {
        public Guid ItemId { get; set; }

        public string? Description { get; set; }

        public int Quantity { get; set; } = 1;

        public decimal UnitPrice { get; set; }

        public decimal? VatRate { get; set; }

        public decimal Discount { get; set; } = 0;
    }
}
