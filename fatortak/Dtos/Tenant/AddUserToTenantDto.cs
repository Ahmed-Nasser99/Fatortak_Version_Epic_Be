namespace fatortak.Dtos.Tenant
{
    public class AddUserToTenantDto
    {
        public string Email { get; set; }
        public string Role { get; set; } = "Viewer";
    }
}
