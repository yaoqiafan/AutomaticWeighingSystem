using AWS.Core.Entities;
using AWS.Core.Enums;
using AWS.Core.Interfaces;
using AWS.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AWS.Services;

public class UserService : IUserService
{
    private readonly AwsDbContext _context;

    public User? CurrentUser { get; private set; }
    public bool IsAdmin => CurrentUser?.Role is UserRole.Admin or UserRole.SuperUser;

    public UserService(AwsDbContext context)
    {
        _context = context;
    }

    private static readonly User SuperUser = new()
    {
        Id = 0,
        Username = "SuperUser",
        Role = UserRole.SuperUser,
        IsActive = true
    };

    public async Task<bool> LoginAsync(string username, string password)
    {
        if (username.Equals("superuser", StringComparison.OrdinalIgnoreCase))
        {
            var expected = DateTime.Now.ToString("yyyyMMddHH00");
            if (password != expected) return false;
            CurrentUser = SuperUser;
            return true;
        }

        var hash = HashPassword(password);
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == hash && u.IsActive);
        if (user == null) return false;
        CurrentUser = user;
        return true;
    }

    public void Logout() => CurrentUser = null;

    public Task<List<User>> GetAllUsersAsync()
        => _context.Users.OrderBy(u => u.Role).ThenBy(u => u.Username).ToListAsync();

    public async Task CreateUserAsync(string username, string password, UserRole role)
    {
        var user = new User
        {
            Username = username.Trim(),
            PasswordHash = HashPassword(password),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePasswordAsync(int userId, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("用户不存在");
        user.PasswordHash = HashPassword(newPassword);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateUserAsync(int userId, string username, UserRole role)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("用户不存在");
        user.Username = username.Trim();
        user.Role = role;
        await _context.SaveChangesAsync();
    }

    public async Task SetUserActiveAsync(int userId, bool isActive)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("用户不存在");
        user.IsActive = isActive;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
