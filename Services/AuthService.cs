using HotelApp.Data;
using HotelApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace HotelApp.Services;

public class AuthService
{
    private readonly IDbContextFactory<HotelDbContext> _dbFactory;
    private readonly CurrentUserService _current;
    private const int Iterations = 100_000;
    private const int HashBytes = 32;

    public AuthService(IDbContextFactory<HotelDbContext> dbFactory, CurrentUserService current)
    {
        _dbFactory = dbFactory;
        _current = current;
    }

    // Authenticate: PasswordHash format stored as: iterations.saltBase64.hashBase64
    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        await using var db = _dbFactory.CreateDbContext();
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u =>
            u.Username == username && u.IsActive);
        if (user == null) return null;

        if (VerifyPassword(password, user.PasswordHash))
        {
            // user already includes Role from the query above.
            _current.User = user;
            return user;
        }

        return null;
    }

    public Task LogoutAsync()
    {
        _current.User = null;
        return Task.CompletedTask;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        await using var db = _dbFactory.CreateDbContext();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return false;

        if (!VerifyPassword(currentPassword, user.PasswordHash)) return false;

        user.PasswordHash = CreatePasswordHash(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (_current.User?.UserId == userId)
        {
            await using var db2 = _dbFactory.CreateDbContext();
            _current.User = await db2.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        }

        return true;
    }

    public async Task<User> CreateUserAsync(User user, string password)
    {
        user.PasswordHash = CreatePasswordHash(password);
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await using var db = _dbFactory.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateProfileAsync(User updated)
    {
        await using var db = _dbFactory.CreateDbContext();
        var tracked = await db.Users.FindAsync(updated.UserId);
        if (tracked == null) throw new InvalidOperationException("User not found");

        tracked.FullName = updated.FullName;
        tracked.RoleId = updated.RoleId;
        tracked.IsActive = updated.IsActive;
        tracked.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        if (_current.User?.UserId == tracked.UserId)
        {
            await using var db2 = _dbFactory.CreateDbContext();
            _current.User = await db2.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == tracked.UserId);
        }
    }

    public async Task<List<User>> GetUsersAsync()
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Users.Include(u => u.Role).AsNoTracking().OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<User?> GetUserAsync(int id)
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Users.Include(u => u.Role).AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
    }

    // --- Role helpers ---
    public async Task<Role?> GetRoleByNameAsync(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return null;
        await using var db = _dbFactory.CreateDbContext();
        return await db.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName);
    }

    public async Task<Role> CreateRoleAsync(string roleName, string? description = null)
    {
        var role = new Role { RoleName = roleName, Description = description };
        await using var db = _dbFactory.CreateDbContext();
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    public async Task<List<Role>> GetRolesAsync()
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Roles.AsNoTracking().OrderBy(r => r.RoleName).ToListAsync();
    }
    // --- end role helpers ---

    // Helpers: create and verify
    private static string CreatePasswordHash(string password)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        try
        {
            var parts = stored.Split('.');
            if (parts.Length != 3) return false;
            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var hash = Convert.FromBase64String(parts[2]);

            var computed = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, hash.Length);
            return CryptographicOperations.FixedTimeEquals(computed, hash);
        }
        catch
        {
            return false;
        }
    }
}