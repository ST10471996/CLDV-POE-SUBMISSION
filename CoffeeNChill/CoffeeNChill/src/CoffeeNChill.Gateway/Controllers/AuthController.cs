using System.Security.Claims;
using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Registration, login and profile endpoints backed by the Users Azure Table.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public AuthController(IUserRepository users, IPasswordHasher hasher, ITokenService tokens)
    { _users = users; _hasher = hasher; _tokens = tokens; }

    /// <summary>Register a new user. Password is BCrypt-hashed before it is stored in the Users table.</summary>
    /// <remarks>Anonymous callers always become <c>Customer</c>. An authenticated Admin may create Staff or Admin users.</remarks>
    /// <response code="201">User created.</response>
    /// <response code="400">Validation failed or invalid role.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var role = Roles.Normalize(req.Role);
        if (role is null) return BadRequest(new ErrorDto("Role must be Admin, Staff or Customer."));
        if (role != Roles.Customer && !User.IsInRole(Roles.Admin)) role = Roles.Customer;

        var user = new UserEntity
        {
            PartitionKey = role,
            RowKey = req.Email.Trim().ToLowerInvariant(),
            PasswordHash = _hasher.Hash(req.Password),
            FullName = req.FullName.Trim(),
            CreatedDate = DateTime.UtcNow
        };
        if (!await _users.CreateAsync(user)) return Conflict(new ErrorDto("Email already registered."));
        return StatusCode(201, new UserDto(user.RowKey, user.FullName, user.PartitionKey, user.CreatedDate));
    }

    /// <summary>Validate credentials and return a signed JWT (Name, Email and Role claims).</summary>
    /// <response code="200">Login succeeded.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !_hasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new ErrorDto("Invalid email or password."));
        var (token, expires) = _tokens.Create(user);
        return Ok(new AuthResponse(token, expires, user.RowKey, user.FullName, user.PartitionKey));
    }

    /// <summary>Return the profile of the currently authenticated user (decoded from the JWT).</summary>
    /// <response code="200">Current user.</response>
    /// <response code="401">Missing or invalid token.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public IActionResult Me() => Ok(new
    {
        Name = User.FindFirstValue(ClaimTypes.Name),
        Email = User.FindFirstValue(ClaimTypes.Email),
        Role = User.FindFirstValue(ClaimTypes.Role)
    });

    /// <summary>List all users (Admin only).</summary>
    [HttpGet("users")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Users() =>
        Ok((await _users.ListAsync()).Select(u => new UserDto(u.RowKey, u.FullName, u.PartitionKey, u.CreatedDate)));
}
