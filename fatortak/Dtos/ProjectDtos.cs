using fatortak.Common.Enum;

namespace fatortak.Dtos
{
    public class ProjectDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public ProjectStatus Status { get; set; }
        public decimal TotalBudget { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateProjectDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Draft;
        public decimal TotalBudget { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class UpdateProjectDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; } // Can update to link/unlink client
        public ProjectStatus Status { get; set; }
        public decimal TotalBudget { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
