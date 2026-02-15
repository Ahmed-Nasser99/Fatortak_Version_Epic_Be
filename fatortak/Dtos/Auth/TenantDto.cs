namespace fatortak.Dtos.Auth
{
    public class TenantDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Subdomain { get; set; }
    }
}
