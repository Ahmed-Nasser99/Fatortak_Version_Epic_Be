namespace fatortak.Dtos.UserProfile
{
    public class UserProfileDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public Guid? TenantId { get; set; }
        public string TenantName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
