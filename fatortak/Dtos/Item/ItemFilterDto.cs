namespace fatortak.Dtos.Item
{
    public class ItemFilterDto
    {
        public string? NameOrCode { get; set; }
        public string? Type { get; set; }
        public bool? IsActive { get; set; }
        public Guid? BranchId { get; set; }
    }
}
