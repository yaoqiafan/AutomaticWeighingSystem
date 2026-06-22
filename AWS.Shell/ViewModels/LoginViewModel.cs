using AWS.Core.Interfaces;
using Prism.Commands;
using Prism.Mvvm;

namespace AWS.Shell.ViewModels;

public class LoginViewModel : BindableBase
{
    private readonly IUserService _userService;

    public event Action? LoginSucceeded;

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password { get; set; } = string.Empty;

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set { SetProperty(ref _errorMessage, value); RaisePropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public DelegateCommand LoginCommand { get; }

    public LoginViewModel(IUserService userService)
    {
        _userService = userService;
        LoginCommand = new DelegateCommand(async () => await ExecuteLoginAsync());
    }

    private async Task ExecuteLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "请输入用户名";
            return;
        }

        var ok = await _userService.LoginAsync(Username.Trim(), Password);
        if (ok)
            LoginSucceeded?.Invoke();
        else
            ErrorMessage = "用户名或密码错误";
    }
}
