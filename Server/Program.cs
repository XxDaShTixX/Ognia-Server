using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Ognia.Server;
using Ognia.Server.Models;
using Ognia.Server.Services.TokenService;
using Ognia.Server.Services.TrackSourceService;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

#region Services
// SQL Lite database connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Injections
builder.Services.AddSingleton<ITrackSourceService, YouTubeTrackSourceService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Identity configuration
builder.Services
    .AddIdentityCore<AppUserModel>()
    .AddSignInManager()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Add controllers
builder.Services.AddControllers();

// Add API explorer for Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();

// Add Swagger/OpenAPI support
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = new List<string>()
    });
});

// JWT configuration
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettingsModel>()!;
if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be configured. Set it via 'dotnet user-secrets set \"Jwt:Key\" \"<your-key>\"' " +
        "or via environment variable 'Jwt__Key'. Key must be at least 32 chars.");
}

builder.Services.Configure<JwtSettingsModel>(builder.Configuration.GetSection("Jwt"));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
    });

// Add authorization
builder.Services.AddAuthorization();
#endregion Services

#region Middleware
// Build the application
var app = builder.Build();

// Apply pending migrations (creates DB and tables if missing)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Enable Swagger/OpenAPI in development environment
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Run the application
app.Run();
#endregion Middleware

