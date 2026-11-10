using System.Reflection;
using System.Text;
using Azure.Data.Tables;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- Options ----------
builder.Services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
var jwt = cfg.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters (set Jwt__Key env var).");

// ---------- Storage (Users table directly via SDK) ----------
var storage = cfg["Storage:ConnectionString"] ?? "UseDevelopmentStorage=true";
builder.Services.AddSingleton(new TableServiceClient(storage));
builder.Services.AddSingleton<IUserRepository, TableUserRepository>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

// ---------- Downstream Azure Functions (Part 1 & 2) ----------
builder.Services.AddHttpClient<IFunctionsClient, FunctionsClient>(c =>
{
    c.BaseAddress = new Uri((cfg["Functions:BaseUrl"] ?? "http://localhost:7071").TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(60);
});

// ---------- Auth ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ---------- Swagger with XML docs + Bearer ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CoffeeNChill API Gateway",
        Version = "v3.0",
        Description = "Unified gateway for menu, staff documents, orders and authentication."
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Paste the token returned by POST /api/auth/login"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// Ensure Users table + seed admin account on startup
using (var scope = app.Services.CreateScope())
{
    var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await users.EnsureCreatedAsync();
    await users.SeedAdminAsync(cfg["Seed:AdminEmail"] ?? "admin@coffeenchill.ac.za",
        cfg["Seed:AdminPassword"] ?? "Admin@12345", hasher);
}

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "CoffeeNChill v3"); c.RoutePrefix = "swagger"; });
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow })).ExcludeFromDescription();

app.Run();

public partial class Program { }
