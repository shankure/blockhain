using Microsoft.EntityFrameworkCore;
using SecureTrace.API.Data;
using SecureTrace.API.DTOs;
using SecureTrace.API.Models;
using SecureTrace.API.Services.Interfaces;

namespace SecureTrace.API.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _config;

    // Only these role values are accepted during registration
    private static readonly HashSet<string> ValidRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "User", "Auditor" };

    public AuthService(AppDbContext db, IJwtService jwt, IConfiguration config)
    {
        _db     = db;
        _jwt    = jwt;
        _config = config;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // 1. Validate role
        if (!ValidRoles.Contains(request.Role))
            throw new ArgumentException($"Invalid role '{request.Role}'. Must be Admin, User, or Auditor.");

        // 2. Check for duplicate email
        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (exists)
            throw new InvalidOperationException($"Email '{request.Email}' is already registered.");

        // 3. Hash the password with BCrypt (work factor 12 is a solid balance for faculty project)
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        // 4. Persist the new user
        var user = new User
        {
            FullName     = request.FullName,
            Email        = request.Email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role         = request.Role,
            CreatedAt    = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // 5. Return a ready-to-use token so the client can log in immediately after registering
        return BuildAuthResponse(user); // Take this newly created user and prepare the login/token response.
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // 1. Look up by email (case-insensitive)
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        // 2. Verify password, same error message for both cases to prevent user enumeration
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        // 3. Block deactivated accounts
        if (!user.IsActive)
            throw new UnauthorizedAccessException("This account has been deactivated.");

        return BuildAuthResponse(user); // Take this newly created user and prepare the login/token response.
    }

    // Private helpers 

    private AuthResponse BuildAuthResponse(User user)
    {
        var expiresInMinutes = int.Parse(_config["Jwt:ExpiresInMinutes"] ?? "60"); // read JWT expiration from configuration
        var token            = _jwt.GenerateToken(user); // calls your JWT service.

        return new AuthResponse(
            Token:     token,
            FullName:  user.FullName,
            Email:     user.Email,
            Role:      user.Role,
            ExpiresAt: DateTime.UtcNow.AddMinutes(expiresInMinutes)
        );
    }
}
