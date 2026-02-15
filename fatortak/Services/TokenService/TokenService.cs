using fatortak.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace fatortak.Services.TokenService
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(ApplicationUser user, Guid? tenantId)
        {
            var claims = new List<Claim>
        {
            new Claim("UserId", user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName),
            tenantId != null ?  new Claim("tenant_id", tenantId.ToString()) : new Claim("" ,""),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            // Add roles (if you're using tenant-specific roles)
            // var roles = await _userManager.GetRolesAsync(user);
            // claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(20),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
