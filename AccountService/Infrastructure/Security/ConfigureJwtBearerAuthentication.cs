using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AccountService.Infrastructure.Security
{
    public static class JwtBearerAuthenticationOptions
    {
        public static IServiceCollection ConfigureJwtBearerAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication("Bearer")
                    .AddJwtBearer("Bearer", options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = "AccountService",
                            ValidAudience = "CashFlowApp",
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes("SuperMegaBlasterSecretKey12345"))
                        };
                    });

            services.AddAuthorization();

            return services;
        }

        public static string Authenticate(string userId, string userName, string email)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName),
                new Claim("email", email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "AccountService",
                audience: "CashFlowApp",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}