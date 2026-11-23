using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Folio.WebBff;

internal static class TestTokenAuthentication
{
    internal const string StaffPolicy = "staff";
    internal const string SubjectClaim = "sub";
    private const string RoleClaim = "role";
    private const string TestSigningKeySetting = "Authentication:TestSigningKey";
    private const string TestIssuerSetting = "Authentication:TestIssuer";
    private const string TestAudienceSetting = "Authentication:TestAudience";

    internal static void AddTestTokenAuthentication(this WebApplicationBuilder builder)
    {
        var testSigningKey = builder.Configuration[TestSigningKeySetting];
        if (!string.IsNullOrEmpty(testSigningKey) && builder.Environment.IsProduction())
        {
            throw new InvalidOperationException(
                $"The Web BFF refuses to start: a test signing key is configured ('{TestSigningKeySetting}') and the environment is Production. Test identity must never reach production.");
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = builder.Configuration[TestIssuerSetting],
                    ValidAudience = builder.Configuration[TestAudienceSetting],
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    IssuerSigningKeys = SigningKeys(testSigningKey),
                    NameClaimType = SubjectClaim,
                    RoleClaimType = RoleClaim,
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, policy => policy.RequireRole(StaffPolicy));
    }

    private static SecurityKey[] SigningKeys(string? testSigningKey) =>
        string.IsNullOrEmpty(testSigningKey) ? [] : [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testSigningKey))];
}
