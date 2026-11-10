using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoffeeNChill.Gateway.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeNChill.Tests;

public class AuthTests
{
    [Fact]
    public void BCrypt_hash_verifies_and_is_not_plain_text()
    {
        var h = new BCryptPasswordHasher();
        var hash = h.Hash("Barista@123");
        Assert.NotEqual("Barista@123", hash);
        Assert.True(h.Verify("Barista@123", hash));
        Assert.False(h.Verify("wrong", hash));
    }

    [Fact]
    public void Jwt_contains_name_email_and_role_claims()
    {
        var svc = new JwtTokenService(Options.Create(new JwtOptions
        {
            Key = "unit-test-key-that-is-definitely-32+-chars", Issuer = "i", Audience = "a", ExpiryMinutes = 5
        }));
        var (token, expires) = svc.Create(new UserEntity
        {
            PartitionKey = "Staff", RowKey = "barista@coffeenchill.ac.za", FullName = "Thabo Nkosi"
        });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Staff");
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Email && c.Value == "barista@coffeenchill.ac.za");
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Name && c.Value == "Thabo Nkosi");
        Assert.True(expires > DateTime.UtcNow);
    }

    [Theory]
    [InlineData("admin", "Admin")]
    [InlineData(" STAFF ", "Staff")]
    [InlineData("customer", "Customer")]
    [InlineData("hacker", null)]
    public void Role_normalisation(string input, string? expected) => Assert.Equal(expected, Roles.Normalize(input));
}
