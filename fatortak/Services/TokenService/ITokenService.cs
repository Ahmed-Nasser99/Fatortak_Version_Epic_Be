using fatortak.Entities;

namespace fatortak.Services.TokenService
{
    public interface ITokenService
    {
        string GenerateToken(ApplicationUser user, Guid? tenantId);
    }
}
