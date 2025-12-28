using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dotNetCrud.Data;
using dotNetCrud.Models;

namespace dotNetCrud.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
                return null;

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return null;

            var token = GenerateJwtToken(user);
            var expirationMinutes = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") 
                ?? _configuration["JwtSettings:ExpirationInMinutes"] 
                ?? "60";
            var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(expirationMinutes));

            return new AuthResponse
            {
                Token = token,
                Email = user.Email,
                Name = user.Name,
                ExpiresAt = expiresAt
            };
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        {
            // Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return null;

            var user = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Name = request.Name,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            var expirationMinutes = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") 
                ?? _configuration["JwtSettings:ExpirationInMinutes"] 
                ?? "60";
            var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(expirationMinutes));

            return new AuthResponse
            {
                Token = token,
                Email = user.Email,
                Name = user.Name,
                ExpiresAt = expiresAt
            };
        }

        public string GenerateJwtToken(User user)
        {
            // Priority: Environment Variable > appsettings.json
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
                ?? _configuration["JwtSettings:SecretKey"] 
                ?? throw new InvalidOperationException("JWT SecretKey not configured");
            
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
                ?? _configuration["JwtSettings:Issuer"] 
                ?? "DotNetCrudAPI";
            
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
                ?? _configuration["JwtSettings:Audience"] 
                ?? "DotNetCrudUsers";
            
            var expirationMinutes = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") 
                ?? _configuration["JwtSettings:ExpirationInMinutes"] 
                ?? "60";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(expirationMinutes)),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

