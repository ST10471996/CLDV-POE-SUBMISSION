using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeNChill.Gateway.Services;

public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "CoffeeNChill.Gateway";
    public string Audience { get; set; } = "CoffeeNChill.Clients";
    public int ExpiryMinutes { get; set; } = 120;
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";
    public const string AdminOrStaff = "Admin,Staff";
    public static readonly string[] All = { Admin, Staff, Customer };
    public static string? Normalize(string? r) =>
        All.FirstOrDefault(x => string.Equals(x, r?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Users table entity. PartitionKey = Role, RowKey = email.</summary>
public class UserEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    public bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); } catch { return false; }
    }
}

public interface IUserRepository
{
    Task EnsureCreatedAsync();
    Task<UserEntity?> FindByEmailAsync(string email);
    Task<bool> CreateAsync(UserEntity user);
    Task<List<UserEntity>> ListAsync();
    Task SeedAdminAsync(string email, string password, IPasswordHasher hasher);
}

public class TableUserRepository : IUserRepository
{
    private readonly TableClient _table;
    public TableUserRepository(TableServiceClient svc) => _table = svc.GetTableClient("Users");

    public Task EnsureCreatedAsync() => _table.CreateIfNotExistsAsync();

    // Email is unique across roles, so search every partition by RowKey.
    public async Task<UserEntity?> FindByEmailAsync(string email)
    {
        var key = email.Trim().ToLowerInvariant();
        await foreach (var u in _table.QueryAsync<UserEntity>(x => x.RowKey == key)) return u;
        return null;
    }

    public async Task<bool> CreateAsync(UserEntity user)
    {
        if (await FindByEmailAsync(user.RowKey) is not null) return false;
        try { await _table.AddEntityAsync(user); return true; }
        catch (RequestFailedException ex) when (ex.Status == 409) { return false; }
    }

    public async Task<List<UserEntity>> ListAsync()
    {
        var list = new List<UserEntity>();
        await foreach (var u in _table.QueryAsync<UserEntity>()) list.Add(u);
        return list;
    }

    public async Task SeedAdminAsync(string email, string password, IPasswordHasher hasher)
    {
        if (await FindByEmailAsync(email) is not null) return;
        await CreateAsync(new UserEntity
        {
            PartitionKey = Roles.Admin, RowKey = email.ToLowerInvariant(),
            PasswordHash = hasher.Hash(password), FullName = "Canteen Manager", CreatedDate = DateTime.UtcNow
        });
    }
}

public interface ITokenService
{
    (string token, DateTime expires) Create(UserEntity user);
}

public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _o;
    public JwtTokenService(IOptions<JwtOptions> o) => _o = o.Value;

    public (string token, DateTime expires) Create(UserEntity user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.RowKey),
            new Claim(ClaimTypes.Role, user.PartitionKey),
            new Claim(JwtRegisteredClaimNames.Sub, user.RowKey),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key)), SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_o.ExpiryMinutes);
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
