using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExamCreateApp.Data;
using ExamCreateApp.Models;
using ExamCreateApp.Models.Auth;

namespace ExamCreateApp.Services;

public class AuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponse> Register(RegisterRequest request)
    {
        try
        {
            // Check if email exists
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email.ToLower()))
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Email is already taken"
                };
            }

            // Check if username exists
            if (await _dbContext.Users.AnyAsync(u => u.Username == request.Username))
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Username is already taken"
                };
            }

            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Create new user
            var user = new User
            {
                Email = request.Email.ToLower(),
                Username = request.Username,
                PasswordHash = passwordHash,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = "user",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"New user registered: {user.Email}");

            // Generate token
            var token = GenerateJwtToken(user);
            var tokenExpiry = DateTime.UtcNow.AddHours(24);

            return new AuthResponse
            {
                Success = true,
                Token = token,
                TokenExpiry = tokenExpiry,
                User = MapToUserDto(user),
                Message = "Registration successful"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return new AuthResponse
            {
                Success = false,
                Message = "An error occurred during registration"
            };
        }
    }

    public async Task<AuthResponse> Login(LoginRequest request)
    {
        try
        {
            // Find user by email
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

            if (user == null)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid email or password"
                };
            }

            // Check if account is active
            if (!user.IsActive)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Account has been deactivated"
                };
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid email or password"
                };
            }

            // Update last login
            user.LastLogin = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Generate token
            var token = GenerateJwtToken(user);
            var tokenExpiry = DateTime.UtcNow.AddHours(24);

            _logger.LogInformation($"User logged in: {user.Email}");

            return new AuthResponse
            {
                Success = true,
                Token = token,
                TokenExpiry = tokenExpiry,
                User = MapToUserDto(user),
                Message = "Login successful"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return new AuthResponse
            {
                Success = false,
                Message = "An error occurred during login"
            };
        }
    }

    public async Task<User?> GetUserById(int userId)
    {
        return await _dbContext.Users.FindAsync(userId);
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("Missing SecretKey in configuration");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("first_name", user.FirstName ?? ""),
            new Claim("last_name", user.LastName ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }
}