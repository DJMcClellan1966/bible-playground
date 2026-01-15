using AI_Bible_App.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

public partial class EmailSignInViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authService;
    
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
    
    public string PageTitle => IsSignInMode ? "Sign In" : "Create Account";
    public string SubmitButtonText => IsSignInMode ? "Sign In" : "Create Account";
    
    public EmailSignInViewModel(IAuthenticationService authService)
    {
        _authService = authService;
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
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
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
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
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
        
        try
        {
            IsBusy = true;
            ClearError();
            
            var sent = await _authService.SendPasswordResetAsync(Email.Trim());
            
            if (sent)
            {
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
    
    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}
