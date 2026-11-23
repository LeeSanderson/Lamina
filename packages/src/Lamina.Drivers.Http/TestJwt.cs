using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lamina.Drivers.Http;

/// <summary>
/// Mints HS256 JWTs for acting as a signed-in user without an identity provider. The key is the UTF-8 bytes of
/// <c>Lamina:TestJwt:SigningKey</c>; <c>Lamina:TestJwt:Issuer</c> and <c>Lamina:TestJwt:Audience</c> are added when set.
/// Claims are <c>sub</c> and one <c>role</c> per role. Tokens live for an hour of real time.
/// </summary>
public sealed class TestJwt
{
    private const string Section = "Lamina:TestJwt";
    private const int MinimumKeyBytes = 32;

    private readonly IConfiguration _configuration;

    public TestJwt(IConfiguration configuration) => _configuration = configuration;

    public string Mint(string subject) => Mint(subject, []);

    public string Mint(string subject, IEnumerable<string> roles)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);
        ArgumentNullException.ThrowIfNull(roles);
        var section = _configuration.GetSection(Section);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = section["Issuer"],
            Audience = section["Audience"],
            SigningCredentials = new SigningCredentials(SigningKey(section), SecurityAlgorithms.HmacSha256),
        });
    }

    private static SymmetricSecurityKey SigningKey(IConfigurationSection section)
    {
        var configured = section["SigningKey"];
        if (string.IsNullOrEmpty(configured))
        {
            throw new InvalidOperationException($"{Section}:SigningKey isn't set. Set it in the suite's configuration to the test key the SUT trusts.");
        }

        var key = Encoding.UTF8.GetBytes(configured);
        return key.Length >= MinimumKeyBytes
            ? new SymmetricSecurityKey(key)
            : throw new InvalidOperationException($"{Section}:SigningKey is {key.Length} bytes, but HS256 needs at least {MinimumKeyBytes}.");
    }
}
