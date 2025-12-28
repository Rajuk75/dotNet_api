using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using dotNetCrud.Data;
using dotNetCrud.Services;
using DotNetEnv;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

// Load .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Load environment variables into configuration
builder.Configuration.AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers();

// Configure Entity Framework - Priority: Environment Variable > appsettings.json
var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=DotNetCrudDb.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // MySQL detection - if contains Port=3306 or User= and Password=
    if (connectionString.Contains("Port=3306") || (connectionString.Contains("User=") && connectionString.Contains("Password=")))
    {
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)));
    }
    // SQLite detection
    else if (connectionString.Contains(".db") || connectionString.Contains("Data Source"))
    {
        options.UseSqlite(connectionString);
    }
    // SQL Server (default)
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// Configure JWT Authentication
// Priority: Environment Variable > appsettings.json
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? builder.Configuration["JwtSettings:SecretKey"] 
    ?? throw new InvalidOperationException("JWT SecretKey not configured");

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
    ?? builder.Configuration["JwtSettings:Issuer"] 
    ?? "DotNetCrudAPI";

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
    ?? builder.Configuration["JwtSettings:Audience"] 
    ?? "DotNetCrudUsers";

var jwtExpiration = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") 
    ?? builder.Configuration["JwtSettings:ExpirationInMinutes"] 
    ?? "60";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// Register custom services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();

// Swagger/OpenAPI - Only for Development (optional, can be removed if not needed)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "User CRUD API",
            Version = "v1",
            Description = "API for User Management with Authentication"
        });

        // Add JWT authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
}

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ============================================
// MIDDLEWARE PIPELINE
// ============================================
app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Authentication & Authorization Middleware
// These run for all requests, but route-level control via [Authorize] / [AllowAnonymous]
app.UseAuthentication();
app.UseAuthorization();

// Map all controllers (routes are defined in Controllers)
// Route-level middleware is controlled by [Authorize] and [AllowAnonymous] attributes
app.MapControllers();

// Ensure database is created and test connection
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var canConnect = context.Database.CanConnect();
        
        if (canConnect)
        {
            Console.WriteLine("✅ Database connected successfully!");
            Console.WriteLine($"📊 Database: {connectionString.Split(';').FirstOrDefault(s => s.Contains("Database="))?.Split('=')[1] ?? "Unknown"}");
        }
        else
        {
            Console.WriteLine("⚠️  Database connection failed. Creating database...");
            context.Database.EnsureCreated();
            Console.WriteLine("✅ Database created successfully!");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database connection error: {ex.Message}");
        throw;
    }
}

app.Run();

