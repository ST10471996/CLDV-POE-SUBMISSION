using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.Dtos;

/// <summary>Registration request.</summary>
public class RegisterRequest
{
    /// <example>barista@coffeenchill.ac.za</example>
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    /// <example>Barista@123</example>
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    /// <example>Thabo Nkosi</example>
    [Required] public string FullName { get; set; } = string.Empty;
    /// <summary>Admin, Staff or Customer. Only an Admin may create Admin/Staff accounts; anonymous registrations become Customer.</summary>
    /// <example>Customer</example>
    public string Role { get; set; } = "Customer";
}

/// <summary>Login request.</summary>
public class LoginRequest
{
    /// <example>admin@coffeenchill.ac.za</example>
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    /// <example>Admin@12345</example>
    [Required] public string Password { get; set; } = string.Empty;
}

/// <summary>JWT returned after successful login.</summary>
public record AuthResponse(string Token, DateTime ExpiresUtc, string Email, string FullName, string Role);

/// <summary>Public user profile (never contains the password hash).</summary>
public record UserDto(string Email, string FullName, string Role, DateTime CreatedDate);

/// <summary>Create menu item.</summary>
public class CreateMenuItemDto
{
    /// <example>Hot Drinks</example>
    [Required] public string Category { get; set; } = string.Empty;
    /// <example>COF-001</example>
    [Required] public string Sku { get; set; } = string.Empty;
    /// <example>Espresso</example>
    [Required] public string Name { get; set; } = string.Empty;
    /// <example>Double shot of rich espresso</example>
    public string? Description { get; set; }
    /// <example>25.00</example>
    [Range(0, 10000)] public double Price { get; set; }
    public bool IsAvailable { get; set; } = true;
}

/// <summary>Update price / availability of a menu item.</summary>
public class UpdateMenuItemDto
{
    /// <example>27.50</example>
    [Range(0, 10000)] public double? Price { get; set; }
    /// <example>true</example>
    public bool? IsAvailable { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

/// <summary>Menu item returned to clients.</summary>
public record MenuItemDto(string Category, string Sku, string Name, string Description, double Price, bool IsAvailable);

/// <summary>Staff document metadata.</summary>
public record StaffDocumentDto(string FileName, long SizeBytes, DateTimeOffset? LastModified);

/// <summary>Place an order (anonymous).</summary>
public class PlaceOrderDto
{
    /// <example>ORD-2026-8801</example>
    public string? OrderId { get; set; }
    /// <example>Jane Smith</example>
    [Required] public string CustomerName { get; set; } = string.Empty;
    /// <example>["COF-001","PAS-104"]</example>
    [Required, MinLength(1)] public List<string> SelectedItemSKUs { get; set; } = new();
    /// <example>65.00</example>
    [Range(0, 100000)] public double TotalPrice { get; set; }
    public DateTime? OrderTimestamp { get; set; }
}

/// <summary>Order as stored in the Orders table.</summary>
public record OrderDto(string OrderDate, string OrderId, string CustomerName, string[] SelectedItemSKUs,
    double TotalPrice, DateTime OrderTimestamp, string Status);

/// <summary>Staff request to change an order's status.</summary>
public class UpdateOrderStatusDto
{
    /// <example>2026-10-12</example>
    [Required] public string OrderDate { get; set; } = string.Empty;
    /// <example>ORD-2026-8801</example>
    [Required] public string OrderId { get; set; } = string.Empty;
    /// <summary>Received, Preparing, Ready or Collected.</summary>
    /// <example>Ready</example>
    [Required] public string Status { get; set; } = string.Empty;
}

/// <summary>Standard error body.</summary>
public record ErrorDto(string Error);
