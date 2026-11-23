using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lamina.Drivers.Http.Tests;

public sealed class TestJwtTests
{
    private const string SigningKey = "folio-acceptance-test-signing-key-not-a-secret";
    private const string Issuer = "folio-acceptance-tests";
    private const string Audience = "folio-web-bff";

    [Fact]
    public async Task AMintedTokenHasTheSubjectAndRolesAndValidatesAgainstTheTestKey()
    {
        var token = await MintAsync(jwt => jwt.Mint("ada-k3f9", ["staff", "auditor"]));

        var result = await ValidateAsync(token, SigningKey);

        result.IsValid.ShouldBeTrue(result.Exception?.Message);
        result.ClaimsIdentity.FindFirst("sub").ShouldNotBeNull().Value.ShouldBe("ada-k3f9");
        result.ClaimsIdentity.FindAll("role").Select(c => c.Value).ShouldBe(["staff", "auditor"]);
    }

    [Fact]
    public async Task ATokenWithNoRolesHasOnlyTheSubject()
    {
        var token = await MintAsync(jwt => jwt.Mint("ada-k3f9"));

        var result = await ValidateAsync(token, SigningKey);

        result.IsValid.ShouldBeTrue(result.Exception?.Message);
        result.ClaimsIdentity.FindAll("role").ShouldBeEmpty();
    }

    [Fact]
    public async Task AMintedTokenDoesNotValidateAgainstAnotherKey()
    {
        var token = await MintAsync(jwt => jwt.Mint("ada-k3f9", ["staff"]));

        var result = await ValidateAsync(token, "some-other-signing-key-of-enough-length");

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task MintingWithoutAConfiguredKeyNamesTheSetting()
    {
        await using var run = await HttpSuite.StartAsync(lamina => lamina.AddTestJwt());
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var jwt = scope.Services.GetRequiredService<TestJwt>();

        Should.Throw<InvalidOperationException>(() => jwt.Mint("ada-k3f9")).Message.ShouldContain("Lamina:TestJwt:SigningKey");
    }

    private static async Task<string> MintAsync(Func<TestJwt, string> mint)
    {
        await using var run = await HttpSuite.StartAsync(lamina =>
        {
            lamina.Configuration["Lamina:TestJwt:SigningKey"] = SigningKey;
            lamina.Configuration["Lamina:TestJwt:Issuer"] = Issuer;
            lamina.Configuration["Lamina:TestJwt:Audience"] = Audience;
            lamina.AddTestJwt();
        });
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        return mint(scope.Services.GetRequiredService<TestJwt>());
    }

    private static Task<TokenValidationResult> ValidateAsync(string token, string key) =>
        new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidIssuer = Issuer,
            ValidAudience = Audience,
        });
}
