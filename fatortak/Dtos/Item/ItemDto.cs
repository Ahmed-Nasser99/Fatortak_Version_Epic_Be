namespace fatortak.Dtos.Item
{
    public class ItemDto
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? PurchaseUnitPrice { get; set; }
        public string? Unit { get; set; }
        public decimal? VatRate { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public int? Quantity { get; set; } = 0;
        public Guid? BranchId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
