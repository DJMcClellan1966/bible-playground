using AI_Bible_App.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;

namespace AI_Bible_App.Maui.ViewModels;

public partial class EmailSignInViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authService;
    private const string SavedUsernameKey = "saved_username";
    private const string SavedPasswordKey = "saved_password";
    private const string UsernameEmailKeyPrefix = "username_email_";
    private string? _savedUsername;
    private string? _savedPassword;
    private bool _promptedUseSaved;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);
    private int _failedAttempts;
    private DateTimeOffset? _lockoutUntil;
    private bool _lockoutTimerRunning;
    
    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string recoveryEmail = string.Empty;
    
    [ObservableProperty]
    private string password = string.Empty;
    
    [ObservableProperty]
    private string confirmPassword = string.Empty;
    
    [ObservableProperty]
    private string errorMessage = string.Empty;
    
    [ObservableProperty]
    private bool hasError;
    
    [ObservableProperty]
    private bool isSignInMode = true;
    
    [ObservableProperty]
    private bool isSignUpMode;

    [ObservableProperty]
    private bool isLockedOut;
    
    public string PageTitle => IsSignInMode ? "Sign In" : "Create Account";
    public string SubmitButtonText => IsSignInMode ? "Sign In" : "Create Account";
    
    public EmailSignInViewModel(IAuthenticationService authService)
    {
        _authService = authService;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _savedUsername = Preferences.Get(SavedUsernameKey, string.Empty);
            _savedPassword = await SecureStorage.GetAsync(SavedPasswordKey);
            _promptedUseSaved = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Failed to load saved credentials: {ex.Message}");
        }
    }

    partial void OnUsernameChanged(string value)
    {
        _ = MaybePromptUseSavedAsync(value);
    }
    
    partial void OnIsSignInModeChanged(bool value)
    {
        IsSignUpMode = !value;
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(SubmitButtonText));
        ClearError();
    }
    
    [RelayCommand]
    private void SetSignInMode()
    {
        IsSignInMode = true;
    }
    
    [RelayCommand]
    private void SetSignUpMode()
    {
        IsSignInMode = false;
        IsSignUpMode = true;
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(SubmitButtonText));
        ClearError();
    }
    
    [RelayCommand]
    private async Task Submit()
    {
        if (IsLockedOut)
        {
            ShowError($"Too many attempts. Try again in {GetRemainingLockoutSeconds()}s.");
            return;
        }

        if (IsSignInMode)
        {
            await SignIn();
        }
        else
        {
            await SignUp();
        }
    }
    
    private async Task SignIn()
    {
        try
        {
            IsBusy = true;
            ClearError();
            
            if (string.IsNullOrWhiteSpace(Username))
            {
                ShowError("Please enter your username");
                return;
            }

            if (Username.Contains('@'))
            {
                ShowError("Username cannot be an email address");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowError("Please enter your password");
                return;
            }
            
            var email = await ResolveEmailForUsernameAsync(Username.Trim());
            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Username not found on this device. Please enter your recovery email to continue.");
                return;
            }

            var result = await _authService.SignInWithEmailAsync(email.Trim(), Password);
            
            if (result.Success)
            {
                ResetLockout();
                await PromptToSaveCredentialsAsync(Username.Trim(), Password);
                SaveUsernameEmailMapping(Username.Trim(), email.Trim());
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                RegisterFailedAttempt();
                ShowError(result.ErrorMessage ?? "Sign in failed");
            }
        }
        catch (Exception ex)
        {
            ShowError($"An error occurred: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task SignUp()
    {
        try
        {
            IsBusy = true;
            ClearError();
            
            if (string.IsNullOrWhiteSpace(Username))
            {
                ShowError("Please choose a username");
                return;
            }

            if (Username.Contains('@'))
            {
                ShowError("Username cannot be an email address");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(RecoveryEmail))
            {
                ShowError("Please enter a recovery email address");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowError("Please enter a password");
                return;
            }
            
            if (Password.Length < 6)
            {
                ShowError("Password must be at least 6 characters");
                return;
            }
            
            if (Password != ConfirmPassword)
            {
                ShowError("Passwords do not match");
                return;
            }
            
            var result = await _authService.SignUpWithEmailAsync(RecoveryEmail.Trim(), Password, Username.Trim());
            
            if (result.Success)
            {
                ResetLockout();
                await PromptToSaveCredentialsAsync(Username.Trim(), Password);
                SaveUsernameEmailMapping(Username.Trim(), RecoveryEmail.Trim());
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                RegisterFailedAttempt();
                ShowError(result.ErrorMessage ?? "Sign up failed");
            }
        }
        catch (Exception ex)
        {
            ShowError($"An error occurred: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task ForgotPassword()
    {
        if (IsLockedOut)
        {
            ShowError($"Too many attempts. Try again in {GetRemainingLockoutSeconds()}s.");
            return;
        }
        
        try
        {
            IsBusy = true;
            ClearError();
            
            var email = RecoveryEmail;
            if (string.IsNullOrWhiteSpace(email))
            {
                var page = GetMainPage();
                if (page != null)
                {
                    email = await page.DisplayPromptAsync(
                        "Recovery Email",
                        "Enter the recovery email for this account:",
                        "Send",
                        "Cancel",
                        keyboard: Keyboard.Email);
                }
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Recovery email is required to reset your password");
                return;
            }

            var sent = await _authService.SendPasswordResetAsync(email.Trim());
            
            if (sent)
            {
                ResetLockout();
                if (Application.Current?.Windows?.Count > 0)
                {
                    var page = Application.Current.Windows[0].Page;
                    if (page != null)
                    {
                        await page.DisplayAlert(
                            "Password Reset",
                            "If an account exists with this email, you will receive password reset instructions.",
                            "OK");
                    }
                }
            }
            else
            {
                RegisterFailedAttempt();
                ShowError("Could not send reset email. Check your email and try again.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Could not send reset email: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task SignInWithGoogle()
    {
        try
        {
            IsBusy = true;
            ClearError();
            
            var result = await _authService.SignInWithGoogleAsync();
            
            if (result.Success)
            {
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Google sign in failed");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Google sign in failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task SignInWithApple()
    {
        try
        {
            IsBusy = true;
            ClearError();
            
            var result = await _authService.SignInWithAppleAsync();
            
            if (result.Success)
            {
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Apple sign in failed");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Apple sign in failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("..");
    }
    
    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    private static async Task SaveCredentialsAsync(string username, string password)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(username))
                Preferences.Set(SavedUsernameKey, username);

            if (!string.IsNullOrWhiteSpace(password))
                await SecureStorage.SetAsync(SavedPasswordKey, password);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Failed to save credentials: {ex.Message}");
        }
    }

    private async Task PromptToSaveCredentialsAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return;

        var page = GetMainPage();
        if (page == null)
        {
            await SaveCredentialsAsync(username, password);
            return;
        }

        var shouldSave = await page.DisplayAlert(
            "Save Login?",
            "Would you like to save your username and password on this device?",
            "Save",
            "Not now");

        if (shouldSave)
        {
            await SaveCredentialsAsync(username, password);
            _savedUsername = username;
            _savedPassword = password;
        }
        else
        {
            _savedUsername = null;
            _savedPassword = null;
        }
    }

    private async Task MaybePromptUseSavedAsync(string currentUsername)
    {
        if (_promptedUseSaved)
            return;

        if (string.IsNullOrWhiteSpace(currentUsername))
            return;

        if (string.IsNullOrWhiteSpace(_savedUsername) || string.IsNullOrWhiteSpace(_savedPassword))
            return;

        // Prompt when typed length reaches a minimum and matches saved username prefix.
        if (currentUsername.Length < 3)
            return;

        if (!_savedUsername.StartsWith(currentUsername, StringComparison.OrdinalIgnoreCase))
            return;

        var page = GetMainPage();
        if (page == null)
            return;

        _promptedUseSaved = true;
        var useSaved = await page.DisplayAlert(
            "Use Saved Login?",
            "We found saved login details for this username. Use them?",
            "Use Saved",
            "Keep Typing");

        if (useSaved)
        {
            Username = _savedUsername;
            Password = _savedPassword;
        }
    }

    private void SaveUsernameEmailMapping(string username, string email)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
            return;

        var key = $"{UsernameEmailKeyPrefix}{username.Trim().ToLowerInvariant()}";
        Preferences.Set(key, email.Trim());
    }

    private async Task<string?> ResolveEmailForUsernameAsync(string username)
    {
        var key = $"{UsernameEmailKeyPrefix}{username.Trim().ToLowerInvariant()}";
        var email = Preferences.Get(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(email))
            return email;

        var page = GetMainPage();
        if (page == null)
            return null;

        var enteredEmail = await page.DisplayPromptAsync(
            "Recovery Email",
            "Enter the recovery email for this username:",
            "Continue",
            "Cancel",
            keyboard: Keyboard.Email);

        if (string.IsNullOrWhiteSpace(enteredEmail))
            return null;

        SaveUsernameEmailMapping(username, enteredEmail);
        RecoveryEmail = enteredEmail;
        return enteredEmail;
    }

    private static Page? GetMainPage()
    {
        if (Application.Current?.Windows?.Count > 0)
            return Application.Current.Windows[0].Page;

        return null;
    }

    private void RegisterFailedAttempt()
    {
        _failedAttempts++;
        if (_failedAttempts >= MaxFailedAttempts)
        {
            _lockoutUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
            IsLockedOut = true;
            StartLockoutTimer();
        }
    }

    private void ResetLockout()
    {
        _failedAttempts = 0;
        _lockoutUntil = null;
        IsLockedOut = false;
        _lockoutTimerRunning = false;
    }

    private int GetRemainingLockoutSeconds()
    {
        if (_lockoutUntil == null)
            return 0;

        var remaining = _lockoutUntil.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            ResetLockout();
            return 0;
        }

        return (int)Math.Ceiling(remaining.TotalSeconds);
    }

    private void StartLockoutTimer()
    {
        if (_lockoutTimerRunning)
            return;

        _lockoutTimerRunning = true;
        ShowError($"Too many attempts. Try again in {GetRemainingLockoutSeconds()}s.");

        Application.Current?.Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (!IsLockedOut)
            {
                _lockoutTimerRunning = false;
                return false;
            }

            var remaining = GetRemainingLockoutSeconds();
            if (remaining <= 0)
            {
                _lockoutTimerRunning = false;
                ClearError();
                return false;
            }

            ShowError($"Too many attempts. Try again in {remaining}s.");
            return true;
        });
    }
    
    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}
