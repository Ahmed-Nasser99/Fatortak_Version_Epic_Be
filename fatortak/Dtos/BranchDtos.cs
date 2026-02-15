namespace fatortak.Dtos
{
    public class BranchDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public bool IsMain { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateBranchDto
    {
        public string Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public bool IsMain { get; set; } = false;
    }

    public class UpdateBranchDto
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public bool? IsMain { get; set; }
        public bool? IsActive { get; set; }
    }
}
