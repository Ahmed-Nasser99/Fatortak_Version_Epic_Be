namespace fatortak.Dtos.Invoice
{
    public class InvoiceItemDto
    {
        public Guid Id { get; set; }
        public Guid? ItemId { get; set; }
        public string? ItemName { get; set; }
        public string? Description { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? VatRate { get; set; }
        public decimal? Discount { get; set; }
        public decimal? LineTotal { get; set; }
    }
}
