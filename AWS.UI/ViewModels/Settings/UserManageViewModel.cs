using AWS.Core.Enums;
using AWS.Core.Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;
using System.Windows;

namespace AWS.UI.ViewModels.Settings;

public class UserManageViewModel : BindableBase, INavigationAware
{
    private readonly IUserService _userService;
    private readonly ILogService _log;

    public ObservableCollection<UserEditItem> Users { get; } = [];

    public IReadOnlyList<UserRole> AvailableRoles { get; } =
        [UserRole.Operator, UserRole.Admin];

    public DelegateCommand LoadUsersCommand { get; }
    public DelegateCommand AddCommand { get; }
    public DelegateCommand<UserEditItem> SaveUserCommand { get; }
    public DelegateCommand<UserEditItem> DeleteCommand { get; }

    public UserManageViewModel(IUserService userService, ILogService log)
    {
        _userService = userService;
        _log = log;

        LoadUsersCommand = new DelegateCommand(async () => await LoadAsync());
        AddCommand       = new DelegateCommand(AddNewUser, () => !Users.Any(u => u.IsNew));
        SaveUserCommand  = new DelegateCommand<UserEditItem>(async item => await SaveAsync(item));
        DeleteCommand    = new DelegateCommand<UserEditItem>(
            async item => await DeleteAsync(item),
            item => item != null &&
                    !(item.Username.Equals(_userService.CurrentUser?.Username,
                        StringComparison.OrdinalIgnoreCase)));
    }

    public void OnNavigatedTo(NavigationContext ctx)
        => System.Windows.Application.Current.Dispatcher.InvokeAsync(
            async () => await LoadAsync(),
            System.Windows.Threading.DispatcherPriority.Loaded);

    public void OnNavigatedFrom(NavigationContext ctx) { }
    public bool IsNavigationTarget(NavigationContext ctx) => false;

    private async Task LoadAsync()
    {
        try
        {
            var list = await _userService.GetAllUsersAsync();
            Users.Clear();
            foreach (var u in list) Users.Add(new UserEditItem(u));
            AddCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _log.Error($"加载用户失败：{ex.Message}", "用户管理");
        }
    }

    private void AddNewUser()
    {
        if (Users.Any(u => u.IsNew)) return;
        Users.Insert(0, new UserEditItem());
        AddCommand.RaiseCanExecuteChanged();
    }

    private async Task SaveAsync(UserEditItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Username))
        {
            MessageBox.Show("用户名不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (item.IsNew)
            {
                if (string.IsNullOrEmpty(item.NewPassword))
                {
                    MessageBox.Show("新用户必须设置密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                await _userService.CreateUserAsync(item.Username, item.NewPassword, item.Role);
                _log.Info($"创建用户：{item.Username} ({item.Role})", "用户管理");
            }
            else
            {
                await _userService.UpdateUserAsync(item.Id, item.Username, item.Role);
                if (!string.IsNullOrEmpty(item.NewPassword))
                {
                    await _userService.UpdatePasswordAsync(item.Id, item.NewPassword);
                    _log.Info($"重置密码：{item.Username}", "用户管理");
                }
                await _userService.SetUserActiveAsync(item.Id, item.IsActive);
                _log.Info($"更新用户：{item.Username}", "用户管理");
            }

            item.NewPassword = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _log.Error($"保存用户失败：{ex.Message}", "用户管理");
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteAsync(UserEditItem item)
    {
        if (item.IsNew)
        {
            Users.Remove(item);
            AddCommand.RaiseCanExecuteChanged();
            return;
        }

        var confirm = MessageBox.Show(
            $"确认删除用户「{item.Username}」？此操作不可撤销。",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _userService.DeleteUserAsync(item.Id);
            Users.Remove(item);
            AddCommand.RaiseCanExecuteChanged();
            _log.Warn($"已删除用户：{item.Username}", "用户管理");
        }
        catch (Exception ex)
        {
            _log.Error($"删除用户失败：{ex.Message}", "用户管理");
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
