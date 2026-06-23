using AWS.Core.Entities;
using AWS.Core.Enums;
using Prism.Mvvm;

namespace AWS.UI.ViewModels.Settings;

public class UserEditItem : BindableBase
{
    public int Id { get; }
    public bool IsNew { get; }
    public DateTime CreatedAt { get; }

    private string _username;
    public string Username { get => _username; set => SetProperty(ref _username, value); }

    private string _newPassword = string.Empty;
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }

    private UserRole _role;
    public UserRole Role { get => _role; set => SetProperty(ref _role, value); }

    private bool _isActive;
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

    public UserEditItem(User user)
    {
        Id = user.Id;
        _username = user.Username;
        _role = user.Role;
        _isActive = user.IsActive;
        CreatedAt = user.CreatedAt;
    }

    public UserEditItem()
    {
        IsNew = true;
        _username = string.Empty;
        _role = UserRole.Operator;
        _isActive = true;
    }
}
