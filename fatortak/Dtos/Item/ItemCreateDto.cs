namespace fatortak.Dtos.Item
{
    public class ItemCreateDto
    {
        public string? Code { get; set; }
        public string Name { get; set; }

        public string? Description { get; set; }

        public string Type { get; set; }

        public decimal? UnitPrice { get; set; }
        public decimal? PurchaseUnitPrice { get; set; }
        public string? Unit { get; set; } = "pcs";
        public decimal? VatRate { get; set; }
        public int? Quantity { get; set; } = 0;

        public Guid? BranchId { get; set; }
        public IFormFile? ImageFile { get; set; }

    }
}
