namespace fatortak.Entities
{
    public class InvoiceItem : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid InvoiceId { get; set; }
        public Guid? ItemId { get; set; }
        public string? Description { get; set; }
        public int? Quantity { get; set; } = 1;
        public decimal? UnitPrice { get; set; }
        public decimal? VatRate { get; set; }
        public decimal? LineTotal { get; set; }
        public decimal? Discount { get; set; }

        public Tenant Tenant { get; set; }
        public Invoice Invoice { get; set; }
        public Item Item { get; set; }
    }
}
