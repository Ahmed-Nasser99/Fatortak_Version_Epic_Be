namespace fatortak.Dtos.Item
{
    public class ItemUpdateDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? PurchaseUnitPrice { get; set; }
        public string? Unit { get; set; }
        public decimal? VatRate { get; set; }
        public int? Quantity { get; set; }
        public bool? IsActive { get; set; }
        public IFormFile? ImageFile { get; set; } // Add this for file upload
        public Guid? BranchId { get; set; }
        public bool? RemoveImage { get; set; } // Optional: Add this to allow removing existing image
    }
}
