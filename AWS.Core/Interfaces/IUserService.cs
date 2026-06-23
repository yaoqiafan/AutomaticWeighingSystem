using AWS.Core.Entities;

namespace AWS.Core.Interfaces;

public interface IUserService
{
    User? CurrentUser { get; }
    bool IsAdmin { get; }
    Task<bool> LoginAsync(string username, string password);
    void Logout();
    Task<List<User>> GetAllUsersAsync();
    Task CreateUserAsync(string username, string password, Enums.UserRole role);
    Task UpdatePasswordAsync(int userId, string newPassword);
    Task UpdateUserAsync(int userId, string username, Enums.UserRole role);
    Task SetUserActiveAsync(int userId, bool isActive);
    Task DeleteUserAsync(int userId);
}
