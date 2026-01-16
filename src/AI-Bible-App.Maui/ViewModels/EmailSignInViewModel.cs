using AI_Bible_App.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;

namespace AI_Bible_App.Maui.ViewModels;

public partial class EmailSignInViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authService;
    private const string SavedEmailKey = "saved_email";
    private const string SavedPasswordKey = "saved_password";
    private string? _savedEmail;
    private string? _savedPassword;
    private bool _promptedUseSaved;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);
    private int _failedAttempts;
    private DateTimeOffset? _lockoutUntil;
    private bool _lockoutTimerRunning;
    
    [ObservableProperty]
    private string email = string.Empty;
    
    [ObservableProperty]
    private string password = string.Empty;
    
    [ObservableProperty]
    private string confirmPassword = string.Empty;
    
    [ObservableProperty]
    private string displayName = string.Empty;
    
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
            _savedEmail = Preferences.Get(SavedEmailKey, string.Empty);
            _savedPassword = await SecureStorage.GetAsync(SavedPasswordKey);
            _promptedUseSaved = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Failed to load saved credentials: {ex.Message}");
        }
    }

    partial void OnEmailChanged(string value)
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
            
            if (string.IsNullOrWhiteSpace(Email))
            {
                ShowError("Please enter your email address");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowError("Please enter your password");
                return;
            }
            
            var result = await _authService.SignInWithEmailAsync(Email.Trim(), Password);
            
            if (result.Success)
            {
                ResetLockout();
                await PromptToSaveCredentialsAsync(Email.Trim(), Password);
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
            
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                ShowError("Please enter your name");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(Email))
            {
                ShowError("Please enter your email address");
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
            
            var result = await _authService.SignUpWithEmailAsync(Email.Trim(), Password, DisplayName.Trim());
            
            if (result.Success)
            {
                ResetLockout();
                await PromptToSaveCredentialsAsync(Email.Trim(), Password);
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
        if (string.IsNullOrWhiteSpace(Email))
        {
            ShowError("Please enter your email address first");
            return;
        }

        if (IsLockedOut)
        {
            ShowError($"Too many attempts. Try again in {GetRemainingLockoutSeconds()}s.");
            return;
        }
        
        try
        {
            IsBusy = true;
            ClearError();
            
            var sent = await _authService.SendPasswordResetAsync(Email.Trim());
            
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

    private static async Task SaveCredentialsAsync(string email, string password)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(email))
                Preferences.Set(SavedEmailKey, email);

            if (!string.IsNullOrWhiteSpace(password))
                await SecureStorage.SetAsync(SavedPasswordKey, password);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Failed to save credentials: {ex.Message}");
        }
    }

    private async Task PromptToSaveCredentialsAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var page = GetMainPage();
        if (page == null)
        {
            await SaveCredentialsAsync(email, password);
            return;
        }

        var shouldSave = await page.DisplayAlert(
            "Save Login?",
            "Would you like to save your email and password on this device?",
            "Save",
            "Not now");

        if (shouldSave)
        {
            await SaveCredentialsAsync(email, password);
            _savedEmail = email;
            _savedPassword = password;
        }
        else
        {
            _savedEmail = null;
            _savedPassword = null;
        }
    }

    private async Task MaybePromptUseSavedAsync(string currentEmail)
    {
        if (_promptedUseSaved)
            return;

        if (string.IsNullOrWhiteSpace(currentEmail))
            return;

        if (string.IsNullOrWhiteSpace(_savedEmail) || string.IsNullOrWhiteSpace(_savedPassword))
            return;

        // Prompt when typed length reaches a minimum and matches saved email prefix.
        if (currentEmail.Length < 5)
            return;

        if (!_savedEmail.StartsWith(currentEmail, StringComparison.OrdinalIgnoreCase))
            return;

        var page = GetMainPage();
        if (page == null)
            return;

        _promptedUseSaved = true;
        var useSaved = await page.DisplayAlert(
            "Use Saved Login?",
            "We found saved login details for this email. Use them?",
            "Use Saved",
            "Keep Typing");

        if (useSaved)
        {
            Email = _savedEmail;
            Password = _savedPassword;
        }
    }

    private static Page? GetMainPage()
    {
        if (Application.Current?.Windows?.Count > 0)
            return Application.Current.Windows[0].Page;

        return Application.Current?.MainPage;
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
